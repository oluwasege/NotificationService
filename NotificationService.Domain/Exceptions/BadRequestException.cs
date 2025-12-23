namespace NotificationService.Domain.Exceptions
{
    public class BadRequestException : RequestErrorException
    {
        public BadRequestException()
            : base()
        {
        }

        public BadRequestException(string message)
                : base(message)
        {
        }

        public BadRequestException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
