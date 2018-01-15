using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AggregationService.Logger
{
    public class RabbitLogger
    {
        private static string path = "RabbitLogs.txt";

        public static async Task LogMessage(string message)
        {
            var corrId = string.Format("{0}", DateTime.Now);

            StreamWriter sw;

            if (!File.Exists(path))
            {
                // Create a file to write to.
                sw = File.CreateText(path);
            }
            else
            {
                sw = File.AppendText(path);
            }

            await Task.Run(() =>
            {
                sw.WriteLine(string.Format("{0}: {1}\r\n", corrId, message));
                sw.WriteLine("\r\n\r\n");
            });

            //Close the file
            sw.Close();
        }
    }
}
