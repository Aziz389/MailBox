using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;
using System.Collections.Generic;
using System.IO;
using IOFile = System.IO.File;
using Redmine.Net.Api;
using Redmine.Net.Api.Types;
using System;




namespace mailBox
{
    public class EmailDetails
    {
        public string EmailFrom { get; set; }
        public string EmailSubject { get; set; }
        public string EmailBody { get; set; }
        public List<string> AttachmentNames { get; set; }
        public List<Attachment> Attachments { get; set; } // Add this property
    }

    public class MimeEmails
    {
        public static List<EmailDetails> ReadMailItems(string server, int port, bool useSsl, string username, string password, string saveDirectory)
        {
            List<EmailDetails> listEmailDetails = new List<EmailDetails>();

            using (var client = new ImapClient())
            {
                //client.Timeout = -1;
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

                            // Create Attachment object and add it to the EmailDetails.Attachments list
                            var attachmentData = new Attachment
                            {
                                FileName = fileName,
                                Content = File.ReadAllBytes(savePath),
                                ContentType = attachment.ContentType.MimeType
                            };
                            emailDetails.Attachments.Add(attachmentData);
                        }
                    }

                    string redmineUrl = "support.acssfax.com";
                    string apiKey = "7b543c489589f420db219addbea683dfc09a107d";
                    RedmineManager redmineManager = new RedmineManager(redmineUrl, apiKey);


                    IdentifiableName Project = new IdentifiableName { Id = 1 };

                    Issue newIssue = new Issue
                    {
                        Subject = emailDetails.EmailSubject,
                        Description = emailDetails.EmailBody,
                        Project = new IdentifiableName { Id = 60 }, // Set the Project ID
                        Tracker = new IdentifiableName { Id = 5 }, // Set the Tracker ID
                        Priority = new IdentifiableName { Id = 2 }, // Set the Priority ID
                        Status = new IdentifiableName { Id = 1 }, // Set the Status ID
                        Attachments = emailDetails.Attachments // Add the attachments to the issue
                    
                };

                    Issue createdIssue = null;
                    try
                    {
                        createdIssue = redmineManager.CreateObject(newIssue);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("problem has occurred during creating issue => " + ex.Message + ",; " + createdIssue);
                    }

                    if (createdIssue != null)
                    {
                        Console.WriteLine("Ticket created successfully. Ticket ID: " + createdIssue.Id);
                    }
                    else
                    {
                        Console.WriteLine("Failed to create a ticket.");
                    }

                    listEmailDetails.Add(emailDetails);
                    inbox.AddFlags(uid, MessageFlags.Seen, true);

                    // Move the email to the destination folder
                    var destinationFolder = inbox.GetSubfolder("Inbox read");
                    inbox.MoveTo(uid, destinationFolder);

                    
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
