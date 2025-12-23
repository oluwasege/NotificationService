using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotificationService.Domain.Exceptions
{
    public class RequestErrorException : Exception
    {

        public RequestErrorException()
        {
        }

        public RequestErrorException(string message)
            : base(message)
        {
        }

        public RequestErrorException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
