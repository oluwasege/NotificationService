using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotificationService.Domain.Exceptions
{
    public class AppDbConcurrencyException : Exception
    {
        public AppDbConcurrencyException()
        {
        }

        public AppDbConcurrencyException(string message)
            : base(message)
        {
        }

        public AppDbConcurrencyException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
