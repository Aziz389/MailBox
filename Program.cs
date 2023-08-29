
using Mailbox;
using System;


namespace mailBox
{
    internal class Program
    {
        static void Main()
        {
            string server = "outlook.office365.com";
            int port = 993;
            bool useSsl = true;
            string username = "ticketsystemacs@outlook.com";
            string password = "Acs04072023";
            string saveDirectory = "D:\\C#\\mailBox\\Attachments";

            while (true)
            {
                EmailUploader.UploadEmailAttachments(server, port, useSsl, username, password, saveDirectory);
               

               

              
            }





        }
    }


 }






