using MimeKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit;
using System.Collections.Generic;
using System.IO;
using IOFile = System.IO.File;
using Redmine.Net.Api;
using Redmine.Net.Api.Types;


namespace mailBox
{
    public class EmailDetails
    {
        public string EmailFrom { get; set; }
        public string EmailSubject { get; set; }
        public string EmailBody { get; set; }
        public List<string> AttachmentNames { get; set; } 

    }

    public class MimeEmails
    {
        public static List<EmailDetails> ReadMailItems(string server, int port, bool useSsl, string username, string password, string saveDirectory)
        {
            List<EmailDetails> listEmailDetails = new List<EmailDetails>();

            using (var client = new ImapClient())
            {
                client.Connect(server, port, useSsl);
                client.Authenticate(username, password);

                var inbox = client.Inbox;
                // Open the inbox with read and write access
                inbox.Open(FolderAccess.ReadWrite);

                var query = SearchQuery.All;
                var uids = inbox.Search(query);

                foreach (var uid in uids)
                {
                    var message = inbox.GetMessage(uid);

                    EmailDetails emailDetails = new EmailDetails
                    {
                        EmailFrom = message.From.ToString(),
                        EmailSubject = message.Subject,
                        EmailBody = message.TextBody,
                        AttachmentNames = new List<string>()
                    };
                    
                    emailDetails.EmailBody = RemoveSignature(message.TextBody);

                    foreach (var attachment in message.Attachments)
                    {
                        var fileName = attachment.ContentDisposition?.FileName;
                        if (fileName != null)
                        {
                            emailDetails.AttachmentNames.Add(fileName);

                            // Save the attachment to the specified directory
                            string savePath = Path.Combine(saveDirectory, fileName);
                            using (var stream = IOFile.Create(savePath))
                            {
                                if (attachment is MimePart mimePart)
                                {
                                    mimePart.Content.DecodeTo(stream);
                                }
                            }
                        }
                    }
                    // Ticket Creation Logic - Start
                    // Connect to the Redmine API using the access key
                    /*string redmineUrl = "https://your-redmine-url.com";
                    string apiKey = "your-api-access-key";
                    RedmineManager redmineManager = new RedmineManager(redmineUrl, apiKey);

                    // Prepare the ticket data
                    Issue newIssue = new Issue
                    {
                        Subject = emailDetails.EmailSubject,
                        Description = emailDetails.EmailBody,
                        // Set other necessary fields, such as project, tracker, priority, assigned to, etc.
                    };

                    // Make an API request to create the ticket
                    //Issue createdIssue = redmineManager.CreateObject(newIssue);

                    if (createdIssue != null)
                    {
                        Console.WriteLine("Ticket created successfully. Ticket ID: " + createdIssue.Id);
                    }
                    else
                    {
                        Console.WriteLine("Failed to create ticket.");
                    }*/
                    

                    listEmailDetails.Add(emailDetails);

                  
                    inbox.AddFlags(uid, MessageFlags.Seen, true);
                }

                client.Disconnect(true);
            }

            return listEmailDetails;
        }
        private static string RemoveSignature(string emailBody)
        {
            if (emailBody == null)
            {
                return null; 
            }

            // Find the index of the first occurrence of either "-" or "_"
            int endIndex = emailBody.IndexOfAny(new[] { '-', '_' });

            
            if (endIndex >= 0)
            {
                emailBody = emailBody.Substring(0, endIndex);
            }

            return emailBody;
        }





    }
}