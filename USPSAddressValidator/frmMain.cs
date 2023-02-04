using System.Data;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Web;
using System.Xml;
using System.Xml.Serialization;
using USPSAddressValidator.Helpers;
using USPSAddressValidator.Models;
using USPSAddressValidator.Request;
using USPSAddressValidator.Response;
using USPSAddressValidator.Utilities;

namespace USPSAddressValidator
{
    public partial class frmMain : Form
    {
        //Constants
        const string TAB = "     ";
        const string USER_ID = "213CVSHE8041";
        static readonly Encoding ISO88591 = Encoding.GetEncoding("ISO-8859-1");

        //API 
        HttpClient httpClient = new HttpClient();
        Stopwatch watch = new Stopwatch();
        List<Task> tasks;
        SemaphoreSlim throttler;

        //XML
        XmlWriterSettings xmlWriterSettings = new XmlWriterSettings
        {
            Indent = true,
            OmitXmlDeclaration = false,
            Encoding = ISO88591
        };
        XmlSerializer xmlConvert = new XmlSerializer(typeof(AddressValidateResponse));

        //Properties
        string TimeStamp => $"{TimeSpan.FromMilliseconds(watch.ElapsedMilliseconds):hh\\:mm\\:ss}";

        //Utilities
        readonly Logger log = new Logger();
        readonly EventUtility stats = new EventUtility();
        readonly PrintUtility print = new PrintUtility();
        readonly FileUtility file = new FileUtility("C:\\Users\\ryand\\OneDrive\\Desktop\\USPSAddressValidator\\USPSAddressValidator\\results.csv");

        //Variables
        DataTable table;
        string baseURL = string.Empty;
        int threadCount = 0;
        int refreshRate = 0;
        int requests = 0;
        int taskDelay = 0;
        List<AddressValidateRequest> requestList;
        StringBuilder results;

        #region Windows Form Events

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();

        public frmMain()
        {
            InitializeComponent();
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            AllocConsole();
            ConsoleUtility.MoveWindowToCenter();
            openFileDialog.FileName = txtFile.Text;
            OpenFile();
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            openFileDialog.ShowDialog();
        }

        private void openFileDialog_FileOk(object sender, System.ComponentModel.CancelEventArgs e)
        {
            OpenFile();
        }


        private void btnGenerate_Click(object sender, EventArgs e)
        {
            Generate();
        }

        #endregion

        /// <summary>
        /// Method which is used to open a CSV file and load it into the DataTable
        /// </summary>
        /// <exception cref="Exception"></exception>
        private void OpenFile()
        {
            if (string.IsNullOrWhiteSpace(openFileDialog.FileName)) return;

            txtFile.Text = openFileDialog.FileName;
            var csv = new CSVUtility(openFileDialog.FileName, false);
            table = csv.Table;

            try
            {
                //TODO: System.Exception: 'Sum of the columns' FillWeight values cannot exceed 65535.'
                if (chkPreviewCSV.Checked)
                    dataGridView.DataSource = table;

                lblRowSize.Text = $"Rows: {table.Rows.Count.ToString("N0")}";
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        /// <summary>
        /// Method which is used to start API requests
        /// </summary>
        private void Generate()
        {
            if (table == null)
            {
                MessageBox.Show("You must load a CSV file.", "Error");
                return;
            }

            GenerateRequests();
            Setup();
            Process();
        }

        /// <summary>
        /// Method which is used to generate a list of requests to be made
        /// </summary>
        private void GenerateRequests()
        {
            const int ADDRESS1 = 0;
            const int ADDRESS2 = 1;
            const int CITY = 2;
            const int STATE = 3;
            const int ZIP5 = 4;
            const int ZIP4 = 5;

            requestList = new List<AddressValidateRequest>();

            foreach (DataRow row in table.Rows)
            {
                var rq = new AddressValidateRequest();
                rq.Address.Address1 = (string)row.ItemArray[ADDRESS1] ?? "";
                rq.Address.Address2 = (string)row.ItemArray[ADDRESS2] ?? "";
                rq.Address.City = (string)row.ItemArray[CITY] ?? "";
                rq.Address.State = (string)row.ItemArray[STATE] ?? "";
                rq.Address.Zip5 = (string)row.ItemArray[ZIP5] ?? "";
                rq.Address.Zip4 = (string)row.ItemArray[ZIP4] ?? "";
                requestList.Add(rq);
            }
        }

        /// <summary>
        /// Method which is used to configure variables before making API requests
        /// </summary>
        private void Setup()
        {
            //Assign options based on form elements
            baseURL = txtBaseURL.Text;
            refreshRate = int.TryParse(txtRefreshRate.Text, out refreshRate) ? refreshRate : 200;
            threadCount = int.TryParse(txtThreadCount.Text, out threadCount) ? threadCount : 3;
            taskDelay = int.TryParse(txtTaskDelay.Text, out taskDelay) ? taskDelay : 1;

            tasks = new List<Task>();
            throttler = new SemaphoreSlim(threadCount);
            results = new StringBuilder();
            xmlWriterSettings = new XmlWriterSettings
            {
                Indent = true,
                OmitXmlDeclaration = false,
                Encoding = ISO88591
            };

            //Initialize
            log.Clear();
            stats.Clear();
            watch.Reset();
            watch.Start();
           
        }

        public async void Process()
        {
            print.Line();
            print.Line($"{TimeStamp}{TAB}Starting...");
            print.Line();

            foreach (var rq in requestList)
            {
                await throttler.WaitAsync();

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var encodedXML = ComposeXML(rq);            //Convert request into ISO-8859-1 encoded XML
                        var response = await GET(encodedXML);       //Sent GET request to API
                        if (!IsValidResponse(response)) return;     //Validate response
                        var rs = ParseResponse(response);           //Convert response into <AddressValidateResponse> model

                        //Add result to collection
                        var result = $"{rs.Address.Address1},{rs.Address.Address2},{rs.Address.City},{rs.Address.State},{rs.Address.Zip5},{rs.Address.Zip4}";
                        results.AppendLine(result);

                        RefreshUI();
                        await Task.Delay(taskDelay);
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex.Message);
                        stats.Exception();
                    }
                    finally
                    {
                        throttler.Release();
                    }

                }));
            }

            //Wait for all the tasks to complete
            await Task.WhenAll(tasks.ToArray());

            watch.Stop();

            SaveResults();
            PrintResults();
        }

        private string ComposeXML(AddressValidateRequest rq)
        {
            string xml
                = $@"<AddressValidateRequest USERID=""{USER_ID}"">"
                + $@"<Revision>1</Revision>"
                + $@"<Address ID=""0"">"
                + $@"<Address1>{rq.Address.Address1}</Address1>"
                + $@"<Address2>{rq.Address.Address2}</Address2>"
                + $@"<City>{rq.Address.City}</City>"
                + $@"<State>{rq.Address.State}</State>"
                + $@"<Zip5>{rq.Address.Zip5}</Zip5>"
                + $@"<Zip4>{rq.Address.Zip4}</Zip4>"
                + $@"</Address>"
                + $@"</AddressValidateRequest>";

            return HttpUtility.UrlEncode(xml, ISO88591);
        }

        private async Task<HttpResponseMessage> GET(string encodedXML)
        {
            return await httpClient.GetAsync($"{baseURL}{encodedXML}");
        }

        private bool IsValidResponse(HttpResponseMessage response)
        {
            if (response == null || !response.IsSuccessStatusCode || response.Content == null)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    stats.NotFound();
                else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    stats.BadRequest();

                return false;
            }

            return true;
        }

        private AddressValidateResponse ParseResponse(HttpResponseMessage response)
        {
            var content = response.Content.ReadAsStringAsync().Result;
            if (string.IsNullOrWhiteSpace(content)) return null;

            AddressValidateResponse rs;
            using (StringReader reader = new StringReader(content))
                rs = (AddressValidateResponse)xmlConvert.Deserialize(reader);

            return rs;
        }

        private void RefreshUI()
        {
            if (refreshRate < 1) return;

            Interlocked.Increment(ref requests);
            if (requests % refreshRate == 0)
            {
                var elapsed = TimeSpan.FromMilliseconds(watch.ElapsedMilliseconds);
                print.Line($"{TimeStamp}{TAB}{requests} requests completed in {elapsed:hh\\:mm\\:ss}");
            }
        }

        private void SaveResults(bool openFile = true)
        {
            file.Save(results.ToString());

            if (openFile)
                file.Open();
        }

        private void PrintResults()
        {
            var elapsed = TimeSpan.FromMilliseconds(watch.ElapsedMilliseconds);
            //var successLog = log.Items.OrderBy(x => x.DateAdded).Where(x => x.Type == LogItemType.Success).Select(x => x.Message).ToList();
            //var errorLog = log.Items.OrderBy(x => x.DateAdded).Where(x => x.Type == LogItemType.Error).Select(x => x.Message).ToList();

            print.Line();
            print.Line($"{TimeStamp}{TAB}Done.");
            print.Line();
            //print.Text(string.Join(Environment.NewLine, successLog));
            //print.Line();
            //print.Text(string.Join(Environment.NewLine, errorLog));

            print.Line($"{TimeStamp}{TAB}{requests:N0} requests completed in {elapsed:hh\\:mm\\:ss}");
        }
    }
}