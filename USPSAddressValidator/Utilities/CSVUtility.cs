//Source: https://social.technet.microsoft.com/wiki/contents/articles/50922.c-importing-csv-content-and-populating-datagridview-in-windows-form-app-with-the-content.aspx

using Microsoft.VisualBasic.FileIO; // This namespace usage is important or else TextFieldParser method will lead to error
using System.Data;

namespace USPSAddressValidator.Utilities
{
    public class CSVUtility
    {
        public DataTable Table;

        public CSVUtility(string fileName, bool firstRowContainsFieldNames = true)
        {
            Table = GenerateDataTable(fileName, firstRowContainsFieldNames);
        }

        private static DataTable GenerateDataTable(string fileName, bool firstRowContainsFieldNames = true)
        {
            DataTable result = new DataTable();

            if (fileName == "")
            {
                return result;
            }

            string delimiters = ",";
            string extension = Path.GetExtension(fileName);

            if (extension.ToLower() == "txt")
                delimiters = "\t";
            else if (extension.ToLower() == "csv")
                delimiters = ",";

            using (TextFieldParser tfp = new TextFieldParser(fileName))
            {
                tfp.SetDelimiters(delimiters);

                // Get The Column Names
                if (!tfp.EndOfData)
                {
                    string[] fields = tfp.ReadFields();

                    for (int i = 0; i < fields.Count(); i++)
                    {
                        if (firstRowContainsFieldNames)
                            result.Columns.Add(fields[i]);
                        else
                            result.Columns.Add("Col" + i);
                    }

                    // If first line is data then add it
                    if (!firstRowContainsFieldNames)
                        result.Rows.Add(fields);
                }

                // Get Remaining Rows from the CSV
                while (!tfp.EndOfData)
                    result.Rows.Add(tfp.ReadFields());
            }

            return result;
        }
    }
}