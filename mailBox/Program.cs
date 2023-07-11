
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
                var mails = MimeEmails.ReadMailItems(server, port, useSsl, username, password, saveDirectory);
                int i = 1;
                foreach (var mail in mails)
                {
                    //Console.WriteLine("Mail No: " + i);
                    Console.WriteLine("Mail Received From: " + mail.EmailFrom);
                    Console.WriteLine("Mail Subject: " + mail.EmailSubject);
                    Console.WriteLine("Mail Body: " + mail.EmailBody);
                    if (mail.AttachmentNames.Count > 0)
                    {
                        Console.WriteLine("Attachments:");
                        foreach (var attachmentName in mail.AttachmentNames)
                        {
                            Console.WriteLine(attachmentName);
                        }
                    }

                    i++;


                }

              
            }





        }
    }


 }






