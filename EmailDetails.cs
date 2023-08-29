using System;
using System.Collections.Generic;
using System.IO;
using MailKit;
using MailKit.Net.Imap;
using MimeKit;
using RestSharp;
using Redmine.Net.Api;
using Redmine.Net.Api.Types;
using MailKit.Search;

namespace Mailbox
{
    public class EmailDetails
    {
        public string EmailFrom { get; set; }
        public string EmailSubject { get; set; }
        public string EmailBody { get; set; }
        public List<string> AttachmentNames { get; set; }
    }

    public class EmailUploader
    {
        private const string RedmineUrl = "https://support.acssfax.com";
        private const string ApiKey = "93ecff0346295b000c5d2602bf55d1ad1592f3a4";

        public static void UploadEmailAttachments(string server, int port, bool useSsl, string username, string password, string saveDirectory)
        {
            using (var client = new ImapClient())
            {
                client.Connect(server, port, useSsl);
                client.Authenticate(username, password);

                var inbox = client.Inbox;
                inbox.Open(FolderAccess.ReadWrite);

                var query = SearchQuery.All;
                var uids = inbox.Search(query);

                Console.WriteLine($"Total emails to process: {uids.Count}");

                foreach (var uid in uids)
                {
                    var message = inbox.GetMessage(uid);

                    EmailDetails emailDetails = new EmailDetails
                    {
                        EmailFrom = message.From.ToString(),
                        EmailSubject = message.Subject,
                        EmailBody = RemoveSignature(message.TextBody),
                        AttachmentNames = new List<string>()
                    };

                    Console.WriteLine($"Processing email with subject: {emailDetails.EmailSubject}");

                    List<string> attachmentTokens = new List<string>(); // To store attachment tokens

                    foreach (var attachment in message.Attachments)
                    {
                        var fileName = attachment.ContentDisposition?.FileName;
                        if (!string.IsNullOrEmpty(fileName))
                        {
                            emailDetails.AttachmentNames.Add(fileName);
                            string savePath = Path.Combine(saveDirectory, fileName);

                            using (var stream = File.Create(savePath))
                            {
                                if (attachment is MimePart mimePart)
                                {
                                    mimePart.Content.DecodeTo(stream);
                                }
                            }

                            byte[] attachmentContent = File.ReadAllBytes(savePath);

                            var restClient = new RestClient(RedmineUrl);
                            var request = new RestRequest("/uploads.json?filename=" + fileName, Method.Post);
                            request.AddHeader("X-Redmine-API-Key", ApiKey);
                            request.AddHeader("Content-Type", "application/octet-stream");

                            request.AddParameter("application/octet-stream", attachmentContent, ParameterType.RequestBody);

                            var response = restClient.Execute(request);

                            if (response.StatusCode == System.Net.HttpStatusCode.Created)
                            {
                                var redmineUpload = Newtonsoft.Json.JsonConvert.DeserializeObject<RedmineUpload>(response.Content);
                                if (redmineUpload != null)
                                {
                                    Console.WriteLine($"Attachment '{fileName}' uploaded successfully. Token: {redmineUpload.Upload.Token}");
                                    attachmentTokens.Add(redmineUpload.Upload.Token); // Store attachment token
                                }
                            }
                            else
                            {
                                Console.WriteLine($"Attachment upload failed. Status code: {response.StatusCode}");
                                Console.WriteLine($"Error message: {response.ErrorMessage}");
                                Console.WriteLine($"Response content: {response.Content}");
                            }
                        }
                    }

                    // Create Redmine issue and associate attachments
                    CreateRedmineIssue(emailDetails, attachmentTokens);

                    inbox.AddFlags(uid, MessageFlags.Seen, true);

                    var destinationFolder = inbox.GetSubfolder("Inbox read");
                    inbox.MoveTo(uid, destinationFolder);

                    Console.WriteLine($"Email with subject '{emailDetails.EmailSubject}' processed and moved to 'Inbox read' folder.");
                }

                client.Disconnect(true);
            }
        }

        private static void CreateRedmineIssue(EmailDetails emailDetails, List<string> attachmentTokens)
        {
            var redmineManager = new RedmineManager(RedmineUrl, ApiKey);

            // Combine attachment tokens into a single string
            string attachmentTokenString = string.Join(", ", attachmentTokens);

            Issue newIssue = new Issue
            {
                Subject = emailDetails.EmailSubject,
                Description = emailDetails.EmailBody,
                Project = new IdentifiableName { Id = 60 },
                Tracker = new IdentifiableName { Id = 5 },
                Priority = new IdentifiableName { Id = 2 },
                Status = new IdentifiableName { Id = 1 },
                Uploads = new List<Upload>()
            };

            foreach (var token in attachmentTokens)
            {
                var upload = new Upload
                {
                    Token = token
                };

                newIssue.Uploads.Add(upload);
            }

            var createdIssue = redmineManager.CreateObject(newIssue);

            if (createdIssue != null)
            {
                Console.WriteLine($"Redmine issue created with ID: {createdIssue.Id}");
            }
            else
            {
                Console.WriteLine("Failed to create Redmine issue.");
            }
        }

        private static string RemoveSignature(string emailBody)
        {
            if (emailBody == null)
            {
                return null;
            }

            int endIndex = emailBody.IndexOfAny(new[] { '-', '_' });

            if (endIndex >= 0)
            {
                emailBody = emailBody.Substring(0, endIndex);
            }

            return emailBody;
        }
    }

    public class RedmineUpload
    {
        public UploadInfo Upload { get; set; }
    }

    public class UploadInfo
    {
        public string Token { get; set; }
    }
}
