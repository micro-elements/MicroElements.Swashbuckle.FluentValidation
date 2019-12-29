using System.Text.Json;
using Newtonsoft.Json.Serialization;

namespace SampleWebApi
{
    /// <summary>
    /// Allows use Newtonsoft <see cref="NamingStrategy"/> as System.Text <see cref="JsonNamingPolicy"/>.
    /// </summary>
    public class NewtonsoftJsonNamingPolicy : JsonNamingPolicy
    {
        private readonly NamingStrategy _namingStrategy;

        /// <summary>
        /// Creates new instance of <see cref="NewtonsoftJsonNamingPolicy"/>.
        /// </summary>
        /// <param name="namingStrategy">Newtonsoft naming strategy.</param>
        public NewtonsoftJsonNamingPolicy(NamingStrategy namingStrategy)
        {
            _namingStrategy = namingStrategy;
        }

        /// <inheritdoc />
        public override string ConvertName(string name)
        {
            return _namingStrategy.GetPropertyName(name, false);
        }
    }
}