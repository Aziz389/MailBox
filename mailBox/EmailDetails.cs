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
using RestSharp;




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



                    string redmineUrl = "https://support.acssfax.com";
                    string apiKey = "ae6bb29efb0ec4769cb40e2a89355f30cb2b81e8";
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


                    };

                    Issue createdIssue = null;
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

                            byte[] attachmentContent = IOFile.ReadAllBytes(savePath);

                            var restClient = new RestClient(redmineUrl);
                            var request = new RestRequest("uploads.json", Method.Post); 

                            request.AddHeader("Content-Type", "application/octet-stream");
                            request.AddParameter("attachment[filename]", fileName, ParameterType.QueryString);
                            request.AddParameter("attachment[token]", apiKey, ParameterType.QueryString);
                            request.AddParameter("application/octet-stream", attachmentContent, ParameterType.RequestBody); // Add the attachment content as a parameter

                            var response = restClient.Execute(request);
                            

                            if (response.IsSuccessful)
                            {
                                // The attachment upload was successful
                                var redmineAttachment = Newtonsoft.Json.JsonConvert.DeserializeObject<Attachment>(response.Content);

                                // Check the redmineAttachment object and proceed accordingly
                                if (redmineAttachment != null)
                                {
                                    // Create an attachment object with the uploaded file's token
                                    Attachment attachmentObj = new Attachment
                                    {
                                        FileName = fileName,
                                        ContentUrl = redmineAttachment.ContentUrl
                                    };

                                    // Add the attachment to the created issue
                                    if (createdIssue.Attachments == null)
                                        createdIssue.Attachments = new List<Attachment>();

                                    createdIssue.Attachments.Add(attachmentObj);
                                }
                            }
                            else
                            {
                                // The attachment upload failed
                                Console.WriteLine("Attachment upload failed. Status code: " + response.StatusCode);
                                Console.WriteLine("Error message: " + response.ErrorMessage);
                                Console.WriteLine("Response content: " + response.Content);
                            }


                            if (response.IsSuccessful)
                            {
                                var redmineAttachment = Newtonsoft.Json.JsonConvert.DeserializeObject<Attachment>(response.Content);

                                if (redmineAttachment != null)
                                {
                                    // Create an attachment object with the uploaded file's token
                                    Attachment attachmentObj = new Attachment
                                    {
                                        FileName = fileName,
                                        ContentUrl = redmineAttachment.ContentUrl
                                    };

                                    // Add the attachment to the created issue
                                    if (createdIssue.Attachments == null)
                                        createdIssue.Attachments = new List<Attachment>();

                                    createdIssue.Attachments.Add(attachmentObj);
                                }
                            }


                        }
                    }
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
