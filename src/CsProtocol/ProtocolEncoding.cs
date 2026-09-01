using System.Text;

namespace CsProtocol;

internal static class ProtocolEncoding
{
    internal static readonly UTF8Encoding Utf8 = new(false, true);
}
