using Com.H.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Com.H.Net.Mail
{
    public class Message : IDisposable
    {

        #region properties
        private bool disposedValue;
        /// <summary>
        /// Delay in milisecond between delete attempts to attachments cache. (default is 1000)
        /// </summary>
        public int CleanupInterval { get; set; }
        /// <summary>
        /// Attempts to cleanup attachements before giving up if they are still locked by other process.
        /// Default is 5
        /// </summary>
        public int CleanupAttempts { get; set; }
        public System.Net.Mail.SmtpDeliveryMethod? DeliveryMethod { get; set; }
        public string SmtpServer { get; set; }
        public int? Port { get; set; }
        public string Uid { get; set; }
        public string Pwd { get; set; }
        public bool Ssl { get; set; }
        public string Subject { get; set; }
        public string From { get; set; }
        public string Body { get; set; }

        #region to

        public List<string> To { get; set; }
        public string ToStr
        {
            get => string.Join(',', this.To);
            set => this.To = value.Split(new char[] { ',', ' ', ';' },
                    StringSplitOptions.RemoveEmptyEntries
                    | StringSplitOptions.TrimEntries).ToList();
        }

        #endregion

        #region cc

        public List<string> Cc { get; set; }
        public string CcStr
        {
            get => string.Join(',', this.Cc);
            set => this.Cc = value.Split(new char[] { ',', ' ', ';' },
                    StringSplitOptions.RemoveEmptyEntries
                    | StringSplitOptions.TrimEntries).ToList();
        }

        #endregion        

        #region bcc
        public List<string> Bcc { get; set; }
        public string BccStr
        {
            get => string.Join(',', this.Bcc);
            set => this.Bcc = value.Split(new char[] { ',', ' ', ';' },
                    StringSplitOptions.RemoveEmptyEntries
                    | StringSplitOptions.TrimEntries).ToList();
        }
        #endregion        

        private System.Net.Mail.MailMessage Msg { get; set; }


        public bool IsHtml { get; set; }

        public AttachmentCollection Attachments => this.Msg.Attachments;

        #endregion

        #region constructor
        public Message()
        {
            this.IsHtml = true;
            this.Ssl = false;
            this.To = new List<string>();
            this.Cc = new List<string>();
            this.Bcc = new List<string>();
            this.CleanupAttempts = 5;
            this.CleanupInterval = 1000;
            this.Msg = new System.Net.Mail.MailMessage();
        }
        #endregion

        

        #region send
        public Task SendAsync(CancellationToken? token = null)
        {
            if (string.IsNullOrWhiteSpace(this.SmtpServer)) 
                throw new MissingFieldException(nameof(this.SmtpServer));

            if (this.Port < 1)
                throw new FormatException($"Invalid port value");

            if (this.From == null) throw new MissingFieldException(nameof(this.From));
            if (!this.From.IsEmail()) throw new FormatException($"{this.From} is not a well formed email");
            this.To = this.To.Where(x => x.IsEmail()).ToList();
            this.Cc = this.Cc.Where(x => x.IsEmail()).ToList();
            this.Bcc = this.Bcc.Where(x => x.IsEmail()).ToList();

            if (this.To == null) throw new MissingFieldException(nameof(this.To));
            if (this.To.Count<1
                && this.Cc.Count < 1
                && this.Bcc.Count <1
                ) throw new MissingFieldException($"At least one valid email should be set for either To, Cc, or Bcc");

            System.Net.Mail.SmtpClient client = 
                new (SmtpServer, (int)Port) { EnableSsl = this.Ssl };

            if (!string.IsNullOrEmpty(this.Pwd))
            {
                string uid = string.IsNullOrWhiteSpace(this.Uid)?this.From:this.Uid;
                if (string.IsNullOrWhiteSpace(uid))
                    throw new MissingFieldException("Either 'From' or 'UId' properties should have a valid email.");
                client.Credentials = new System.Net.NetworkCredential(uid, this.Pwd);
            }
            if (this.DeliveryMethod != null) 
                client.DeliveryMethod = (System.Net.Mail.SmtpDeliveryMethod)DeliveryMethod;

            this.Msg.From = new System.Net.Mail.MailAddress(this.From);
            this.To.ForEach(x => this.Msg.To.Add(x));
            this.Cc.ForEach(x => this.Msg.CC.Add(x));
            this.Bcc.ForEach(x => this.Msg.Bcc.Add(x));
            // this.Attachments.ForEach(x => msg.Attachments.Add(new System.Net.Mail.Attachment(x.FilePath)));
            this.Msg.IsBodyHtml = this.IsHtml;
            this.Msg.Body = this.Body;

            if (!string.IsNullOrEmpty(this.Subject))
                this.Msg.Subject = this.Subject
                    .Replace("\r\n", " ")
                    .Replace("\n\r", " ")
                    .Replace("\r", " ")
                    .Replace("\n", " ");
            var task = (token == null ? client.SendMailAsync(this.Msg)
                : client.SendMailAsync(this.Msg, (CancellationToken)token));
            task.ConfigureAwait(true);
            return task;
        }

        public void Send(CancellationToken? token = null)
        {
            this.SendAsync(token).GetAwaiter().GetResult();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    try
                    {
                        foreach(var item in this.Msg.Attachments)
                        {
                            item.Dispose();
                        }
                        this.Msg.Attachments.Clear();
                        this.Msg.Attachments.Dispose();
                        this.Msg.Dispose();

                    }
                    catch { }
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~Message()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion

    }
}
