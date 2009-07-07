using System;
using System.Collections.Generic;
using System.Text;
using Growl.Connector;

namespace Growl
{
    [Serializable]
    public class GNTPForwardComputer : ForwardComputer
    {
        private string ipAddress;
        private int port;
        private string password;

        public GNTPForwardComputer(string description, bool enabled, string ipAddress, int port, string password) 
            : base(description, enabled)
        {
            this.ipAddress = ipAddress;
            this.port = port;
            this.password = password;
        }

        public override bool Available
        {
            get
            {
                return (this.IPAddress != null);
            }
            protected set
            {
                throw new NotSupportedException("The .Available property is read-only.");
            }
        }

        public string IPAddress
        {
            get
            {
                return this.ipAddress;
            }
            protected set
            {
                this.ipAddress = value;
            }
        }

        public int Port
        {
            get
            {
                return this.port;
            }
            protected set
            {
                this.port = value;
            }
        }

        public string Password
        {
            get
            {
                return this.password;
            }
            protected set
            {
                this.password = value;
            }
        }

        public override string AddressDisplay
        {
            get
            {
                if (this.Available)
                    return String.Format("GNTP {0}:{1} {2}", this.ipAddress, this.port, (this.AdditionalOnlineDisplayInfo != null ? String.Format("({0})", this.AdditionalOnlineDisplayInfo) : null));
                else
                    return String.Format("(offline) {0}", (this.AdditionalOfflineDisplayInfo != null ? String.Format("- {0}", this.AdditionalOfflineDisplayInfo) : null));
            }
        }

        public override ForwardComputer Clone()
        {
            GNTPForwardComputer clone = new GNTPForwardComputer(this.Description, this.Enabled, this.IPAddress, this.Port, this.Password);
            return clone;
        }

        internal override void ForwardRegistration(Growl.Connector.Application application, List<Growl.Connector.NotificationType> notificationTypes, Growl.Daemon.RequestInfo requestInfo)
        {
            Forwarder growl = new Forwarder(this.Password, this.IPAddress, this.Port, requestInfo);
            growl.KeyHashAlgorithm = this.HashAlgorithm;
            growl.EncryptionAlgorithm = this.EncryptionAlgorithm;
            growl.Register(application, notificationTypes.ToArray());
        }

        internal override void ForwardNotification(Growl.Connector.Notification notification, Growl.Daemon.CallbackInfo callbackInfo, Growl.Daemon.RequestInfo requestInfo, Forwarder.ForwardedNotificationCallbackHandler callbackFunction)
        {
            Forwarder growl = new Forwarder(this.Password, this.IPAddress, this.Port, requestInfo, callbackInfo);
            growl.KeyHashAlgorithm = this.HashAlgorithm;
            growl.EncryptionAlgorithm = this.EncryptionAlgorithm;
            growl.ForwardedNotificationCallback += callbackFunction;
            growl.Notify(notification, callbackInfo.Context);
        }
    }
}
