namespace Academikus.AgenteInteligenteMentoresTareas.Business.Common.CustomExceptions
{
    /// <summary>
    /// Clase de excepción para manejar errores de validación y mensajes
    /// </summary>
    public class ValidationAndMessageException : Exception
    {
        public ValidationAndMessageException()
        {
        }

        public ValidationAndMessageException(string message)
            : base(message)
        {
        }

        public ValidationAndMessageException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
