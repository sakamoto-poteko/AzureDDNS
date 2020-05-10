using System;
using System.Runtime.Serialization;

namespace AzureDDNS.Services
{
    [Serializable]
    internal class RecordNotExistExcpetion : Exception
    {
        public RecordNotExistExcpetion()
        {
        }

        public RecordNotExistExcpetion(string message) : base(message)
        {
        }

        public RecordNotExistExcpetion(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected RecordNotExistExcpetion(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}