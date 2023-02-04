using USPSAddressValidator.Models;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using System.Net.Http;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Runtime.InteropServices;
using USPSAddressValidator.Extensions;
using USPSAddressValidator.Response;
using USPSAddressValidator.Request;
using System.Xml.Serialization;
using System.Text;
using Microsoft.VisualBasic;
using System.Xml;

namespace USPSAddressValidator
{
    public partial class frmMain : Form
    {
        // IHttpClientFactory clientFactory;
        private static HttpClient httpClient = new HttpClient();


        public frmMain()
        {
            //var serviceProvider = new ServiceCollection().AddHttpClient().BuildServiceProvider();
            //clientFactory = serviceProvider.GetService<IHttpClientFactory>();


            InitializeComponent();
        }

        private string TimeStamp => $"{TimeSpan.FromMilliseconds(watch.ElapsedMilliseconds):hh\\:mm\\:ss}";

        Logger _log = new Logger();
        Statistics _stats = new Statistics();

        //FileWriter fileWriter = new FileWriter("C:\\Users\\ryand\\OneDrive\\Desktop\\USPSAddressValidator\\USPSAddressValidator\\results.csv");

        DataTable table = new DataTable();
        string baseURL = string.Empty;
        int tableSize = 0;
        int threadCount = 0;
        int pingRate = 0;
        int requests = 0;
        int taskDelay = 0;

        TimeSpan elapsed;
        Stopwatch watch = new Stopwatch();
        const string TAB = "     ";
        List<AddressValidateRequest> addressRequestList = new List<AddressValidateRequest>();

        #region Windows Form Events

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();

        private void frmMain_Load(object sender, EventArgs e)
        {
            AllocConsole();
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

        private void OpenFile()
        {
            if (string.IsNullOrWhiteSpace(openFileDialog.FileName)) return;

            txtFile.Text = openFileDialog.FileName;
            var csv = new ReadCSV(openFileDialog.FileName, false);
            table = csv.Table;

            try
            {
                //TODO: System.Exception: 'Sum of the columns' FillWeight values cannot exceed 65535.'


                if (chkPreviewCSV.Checked)
                    dataGridView.DataSource = table;

                tableSize = table.Rows.Count * table.Columns.Count;
                lblTableSize.Text = tableSize.ToString("N0");
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        private void ParseDataTable()
        {
            addressRequestList = new List<AddressValidateRequest>();

            AddressValidateRequest rq;
            foreach (DataRow row in table.Rows)
            {
                rq = new AddressValidateRequest();
                rq.Address.Address1 = (string?)row.ItemArray[0] ?? null;
                rq.Address.Address2 = (string?)row.ItemArray[1] ?? null;
                rq.Address.City = (string?)row.ItemArray[2] ?? null;
                rq.Address.State = (string?)row.ItemArray[3] ?? null;
                rq.Address.Zip5 = (string?)row.ItemArray[4] ?? null;
                rq.Address.Zip4 = (string?)row.ItemArray[5] ?? null;
                addressRequestList.Add(rq);
            }
        }

        private void Generate()
        {
            if (table == null)
            {
                MessageBox.Show("You must load a CSV file.", "Error");
                return;
            }

            //Assign options based on form elements
            baseURL = txtBaseURL.Text;
            pingRate = int.TryParse(txtPingRate.Text, out pingRate) ? pingRate : 200;
            threadCount = int.TryParse(txtThreadCount.Text, out threadCount) ? threadCount : 3;
            taskDelay = int.TryParse(txtTaskDelay.Text, out taskDelay) ? taskDelay : 1;

            ParseDataTable();
            ProcessAsync();
        }

        public async void ProcessAsync()
        {
            //Initialize
            _log.Clear();
            _stats.Clear();
            watch.Reset();
            watch.Start();

            var tasks = new List<Task>();

            var throttler = new SemaphoreSlim(threadCount);

            WriteLine($"{Environment.NewLine}{TimeStamp}{TAB}Starting...");


            var encoding = Encoding.GetEncoding("ISO-8859-1");
            XmlWriterSettings xmlWriterSettings = new XmlWriterSettings
            {
                Indent = true,
                OmitXmlDeclaration = false,
                Encoding = encoding
            };

            XmlSerializer xmlSerializer = new XmlSerializer(typeof(AddressValidateRequest));

            XmlSerializer xmlDeserializer = new XmlSerializer(typeof(AddressValidateResponse));

            foreach (var rq in addressRequestList)
            {
                await throttler.WaitAsync();
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        string encodedXML;
                        using (var stream = new MemoryStream())
                        {
                            using (var xmlWriter = XmlWriter.Create(stream, xmlWriterSettings))
                            {
                                //xmlSerializer.Serialize(xmlWriter, obj, ns);
                                xmlSerializer.Serialize(xmlWriter, rq);
                            }
                            encodedXML = encoding.GetString(stream.ToArray());
                        }



                        // using (StringReader reader = new StringReader(xml))
                        // {
                        //    var test = (AddressValidateRequest)serializer.Deserialize(reader);
                        // }

                       
                        var response = await GetAsync(encodedXML);
                        if (response == null || !response.IsSuccessStatusCode || response.Content == null)
                        {
                            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                                _stats.NotFound();
                            else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                                _stats.BadRequest();

                            //_log.Error($"Request failed: ID: {id}{TAB}{response.StatusCode.ToString()}", id);
                            return;
                        }

                        var content = response.Content.ReadAsStringAsync().Result;
                        if (string.IsNullOrWhiteSpace(content)) return;

                        AddressValidateResponse rs;
                        using (StringReader reader = new StringReader(content))
                            rs = (AddressValidateResponse)xmlDeserializer.Deserialize(reader);
                        if (rs == null) return;

                        _log.Success($"{TAB}Address: {rs.Address}", -1);
                        _stats.Success();

                        if (pingRate > 0)
                        {
                            Interlocked.Increment(ref requests);
                            if (requests % pingRate == 0)
                                WriteLine($"{TimeStamp}{TAB}{requests} requests completed in {TimeSpan.FromMilliseconds(watch.ElapsedMilliseconds):hh\\:mm\\:ss}");
                        }

                        // let's wait here for X to honor the API's rate limit                         
                        await Task.Delay(taskDelay);
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex.Message, -1);
                        _stats.Exception();
                    }
                    finally
                    {
                        throttler.Release();
                    }

                }));
            }

            // await for all the tasks to complete
            await Task.WhenAll(tasks.ToArray());

            WriteLine($"{Environment.NewLine}{TimeStamp}{TAB}Done.");

            //Finalize
            watch.Stop();
            elapsed = TimeSpan.FromMilliseconds(watch.ElapsedMilliseconds);
            PrintResults();
        }

        private async Task<HttpResponseMessage> GetAsync(string encodedXML)
        {
            return await httpClient.GetAsync($"{baseURL}{encodedXML}");
        }

        private void PrintResults()
        {
            var successLog = _log.Items.OrderBy(x => x.ID).Where(x => x.Type == LogItemType.Success).Select(x => x.Message).ToList();
            var errorLog = _log.Items.OrderBy(x => x.ID).Where(x => x.Type == LogItemType.Error).Select(x => x.Message).ToList();

            NewLine();
            Write(string.Join(Environment.NewLine, successLog), 1);
            WriteLine($"INFO:{TAB}{requests:N0} requests completed in {elapsed:hh\\:mm\\:ss}");
            Write(string.Join(Environment.NewLine, errorLog), 1);

            //Print Metrics
            if (elapsed.Seconds > 0 && elapsed.Seconds < 60)
            {
                float multiplier = 60 / elapsed.Seconds;
                WriteLine($"INFO:{TAB}{requests * multiplier:N0} requests completed per minute");
                WriteLine($"INFO:{TAB}{requests * multiplier * 60:N0} requests completed in 1 hour");
                WriteLine($"INFO:{TAB}{requests * multiplier * 60 * 2:N0} requests completed in 2 hours");
                WriteLine($"INFO:{TAB}{requests * multiplier * 60 * 3:N0} requests completed in 3 hours");
                WriteLine($"INFO:{TAB}{requests * multiplier * 60 * 8:N0} requests completed in 8 hours");
                NewLine();
            }

            var ids = _log.Items.OrderBy(x => x.ID).Where(x => x.Type == LogItemType.Success).Select(x => x.ID).ToList();
            Write(string.Join(',', ids));

        }

        private void Write(string s = "", int newLines = 0)
        {
            Console.Out.WriteAsync(s);
            Console.Out.WriteAsync($"{Environment.NewLine.Repeat(newLines)}");
        }

        private void WriteLine(string s = "")
        {
            Write(s, 1);
        }


        private void NewLine(int newLines = 1)
        {
            Console.Out.WriteAsync($"{Environment.NewLine.Repeat(newLines)}");
        }

        private string PluralSuffix(int count)
        {
            return count == 1 ? "" : "s";
        }

        private bool IsNumeric(string s)
        {
            return int.TryParse(s, out _);
        }




    }
}