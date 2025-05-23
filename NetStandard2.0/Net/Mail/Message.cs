﻿using Com.H.Threading;
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
        public System.Net.Mail.SmtpDeliveryMethod? DeliveryMethod { get; set; }
        public string SmtpServer { get; set; }
        public int? Port { get; set; }
        public string Uid { get; set; }
        public string Pwd { get; set; }
        public bool Ssl { get; set; } = false;
        public string Subject { get; set; }
        public string From { get; set; }
        public string FromDisplayName { get; set; }
        public Encoding FromDisplayNameEncoding { get; set; }
        public string Body { get; set; }

        #region to

        public List<string> To { get; private set; } = new List<string>();
        public string ToStr
        {
            get => this.To is null?null:string.Join(",", this.To);
            set
            {
                if (string.IsNullOrWhiteSpace(value)) 
                {
                    this.To.Clear();
                    return;
                }

                foreach (var email in value.Split(new char[] { ',', ' ', ';', '\r', '\n' },
                  StringSplitOptions.RemoveEmptyEntries)
                    .Select(x=>x?.Trim())
                    .Where(x =>
                  !string.IsNullOrWhiteSpace(x)
                  ))
                    this.To.Add(email);
                    
            }
        }

        #endregion

        #region cc

        public List<string> Cc { get; private set; } = new List<string>();
        public string CcStr
        {
            get => this.Cc is null?null:string.Join(",", this.Cc);
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    this.Cc.Clear();
                    return;
                }

                foreach (var email in value.Split(new char[] { ',', ' ', ';', '\r', '\n' },
                  StringSplitOptions.RemoveEmptyEntries)
                    .Select(x=>x?.Trim())
                    .Where(x =>
                  !string.IsNullOrWhiteSpace(x)
                  ))
                    this.Cc.Add(email);

            }
        }

        #endregion        

        #region bcc
        public List<string> Bcc { get; private set; } = new List<string>();
        public string BccStr
        {
            get => this.Bcc is null?null:string.Join(",", this.Bcc);
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    this.Bcc.Clear();
                    return;
                }

                foreach (var email in value.Split(new char[] { ',', ' ', ';', '\r', '\n' },
                  StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x?.Trim())
                    .Where(x =>
                  !string.IsNullOrWhiteSpace(x)
                  ))
                    this.Bcc.Add(email);

            }
        }
        #endregion        

        private System.Net.Mail.MailMessage Msg { get;  } = new System.Net.Mail.MailMessage();

        /// <summary>
        /// Default: true
        /// </summary>
        public bool IsHtml { get; set; }

        public MailAttachmentCollection Attachments { get; private set; }

        #endregion

        #region constructor
        public Message()
        {
            this.IsHtml = true;
            this.Ssl = false;
            this.To = new List<string>();
            this.Cc = new List<string>();
            this.Bcc = new List<string>();

            this.Msg = new System.Net.Mail.MailMessage();
            this.Attachments = new MailAttachmentCollection();
        }
        #endregion

        

        #region send
        public async Task SendAsync()
        {
            if (this.disposedValue) throw new ObjectDisposedException("Message");
            if (string.IsNullOrWhiteSpace(this.SmtpServer)) 
                throw new MissingFieldException(nameof(this.SmtpServer));
            if (this.Port is null) this.Port = 21;

            if (this.Port < 1)
                throw new FormatException($"Invalid port value");

            if (this.From == null) throw new MissingFieldException(nameof(this.From));
            if (!this.From.IsEmail()) throw new FormatException($"{this.From} is not a well formed email");
            this.To = this.To.Where(x => x.IsEmail()).ToList();
            this.Cc = this.Cc.Where(x => x.IsEmail()).ToList();
            this.Bcc = this.Bcc.Where(x => x.IsEmail()).ToList();

            //if (this.To == null) throw new MissingFieldException(nameof(this.To));
            if ((this.To == null || this.To.Count<1)
                && (this.Cc == null || this.Cc.Count < 1)
                && (this.Bcc == null || this.Bcc.Count <1)
                ) throw new MissingFieldException($"At least one valid email should be set for either To, Cc, or Bcc");

            SmtpClient client = 
                new SmtpClient(SmtpServer, (int)Port) { EnableSsl = this.Ssl };

            if (!string.IsNullOrEmpty(this.Pwd))
            {
                string uid = string.IsNullOrWhiteSpace(this.Uid)?this.From:this.Uid;
                if (string.IsNullOrWhiteSpace(uid))
                    throw new MissingFieldException("Either 'From' or 'UId' properties should have a valid email.");
                client.Credentials = new System.Net.NetworkCredential(uid, this.Pwd);
            }
            if (this.DeliveryMethod != null) 
                client.DeliveryMethod = (System.Net.Mail.SmtpDeliveryMethod)DeliveryMethod;

            if (this.FromDisplayName != null)
            {
                this.Msg.From = this.FromDisplayNameEncoding is null ?
                    new System.Net.Mail.MailAddress(this.From, this.FromDisplayName)
                    : new System.Net.Mail.MailAddress(this.From, this.FromDisplayName, this.FromDisplayNameEncoding);

            }
            else this.Msg.From = new System.Net.Mail.MailAddress(this.From);


            this.Msg.From = new System.Net.Mail.MailAddress(this.From);
            this.To?.ForEach(x => this.Msg.To.Add(x));
            this.Cc?.ForEach(x => this.Msg.CC.Add(x));
            this.Bcc?.ForEach(x => this.Msg.Bcc.Add(x));
            foreach(var item in this.Attachments.List)
                if (item.Stream != null)
                    this.Msg.Attachments.Add(new System.Net.Mail.Attachment(item.Stream, item.FileName));

            this.Msg.IsBodyHtml = this.IsHtml;
            this.Msg.Body = this.Body;

            if (!string.IsNullOrEmpty(this.Subject))
                this.Msg.Subject = this.Subject
                    .Replace("\r\n", " ")
                    .Replace("\n\r", " ")
                    .Replace("\r", " ")
                    .Replace("\n", " ");
            await client.SendMailAsync(this.Msg);
        }

        public void Send()
        {
            this.SendAsync().GetAwaiter().GetResult();
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

                        this.Attachments.Dispose();

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
