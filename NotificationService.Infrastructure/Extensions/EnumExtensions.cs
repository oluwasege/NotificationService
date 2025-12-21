using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotificationService.Infrastructure.Extensions
{
    public static class EnumExtensions
    {
        public static string GetDescription(this Enum value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value), "Enum value cannot be null.");

            Type type = value.GetType();
            string name = Enum.GetName(type, value);

            if (name == null)
                return value.ToString(); // Return the enum value as a string if no name is found.

            var field = type.GetField(name);
            if (field == null)
                return value.ToString(); // Return the enum value as a string if no field is found.

            var attr = Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) as DescriptionAttribute;
            return attr?.Description ?? value.ToString(); // Return the description or the enum value as a string.
        }
    }
}
