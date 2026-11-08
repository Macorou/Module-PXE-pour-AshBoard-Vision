using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
namespace ProxmoxMonitor.Tests.Support;
internal sealed class LocalTlsServer : IAsyncDisposable
{
    private readonly TcpListener _listener=new(IPAddress.Loopback,0);
    private readonly CancellationTokenSource _stop=new();
    private readonly ConcurrentBag<Task> _clients=new();
    private readonly List<X509Certificate2> _owned=[];
    private readonly Task _accept;
    private X509Certificate2 _certificate;
    internal int Port=>((IPEndPoint)_listener.LocalEndpoint).Port;
    internal X509Certificate2 Certificate=>_certificate;
    internal ConcurrentQueue<bool> AuthorizationSeen {get;}=new();
    internal ConcurrentQueue<string> Errors {get;}=new();
    internal string? RedirectTo {get;set;}
    internal LocalTlsServer(X509Certificate2? shared=null)
    {
        _certificate=shared??NewCertificate();if(shared is null)_owned.Add(_certificate);
        _listener.Start();_accept=AcceptAsync();
    }
    internal void RotateCertificate(){var cert=NewCertificate();_owned.Add(cert);Volatile.Write(ref _certificate,cert);}
    private static X509Certificate2 NewCertificate()
    {
        using var rsa=RSA.Create(2048);
        var request=new CertificateRequest("CN=localhost",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        var san=new SubjectAlternativeNameBuilder();san.AddDnsName("localhost");san.AddIpAddress(IPAddress.Loopback);request.CertificateExtensions.Add(san.Build());
        using var ephemeral=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddDays(1));
        // SChannel needs a provider-backed key. Import into a lifetime-scoped key container,
        // without adding the certificate to any Windows certificate/trust store.
        return X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pkcs12),null,X509KeyStorageFlags.DefaultKeySet);
    }
    private async Task AcceptAsync()
    {
        try {while(!_stop.IsCancellationRequested){var client=await _listener.AcceptTcpClientAsync(_stop.Token);_clients.Add(ServeAsync(client));}}
        catch(Exception ex) when(ex is OperationCanceledException or SocketException or ObjectDisposedException) { }
    }
    private async Task ServeAsync(TcpClient client)
    {
        using(client)
        using(var ssl=new SslStream(client.GetStream()))
        {
            try
            {
                await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions {ServerCertificate=Volatile.Read(ref _certificate)},_stop.Token);
                using var reader=new StreamReader(ssl,Encoding.ASCII,false,1024,true);
                var first=await reader.ReadLineAsync(_stop.Token);if(first is null)return;
                var authorized=false;string? header;
                while(!string.IsNullOrEmpty(header=await reader.ReadLineAsync(_stop.Token)))authorized|=header.StartsWith("Authorization:",StringComparison.OrdinalIgnoreCase);
                AuthorizationSeen.Enqueue(authorized);
                var body="{\"data\":{\"version\":\"9.0-test\"}}";
                var response=RedirectTo is {} location?"HTTP/1.1 302 Found\r\nLocation: "+location+"\r\nContent-Length: 0\r\nConnection: close\r\n\r\n":
                    "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: "+Encoding.UTF8.GetByteCount(body)+"\r\nConnection: close\r\n\r\n"+body;
                await ssl.WriteAsync(Encoding.UTF8.GetBytes(response),_stop.Token);
            }
            catch(Exception ex) when(ex is AuthenticationException or IOException or OperationCanceledException) { Errors.Enqueue(ex.GetType().Name+": "+ex.Message); }
        }
    }
    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();_listener.Stop();await _accept;
        await Task.WhenAll(_clients).WaitAsync(TimeSpan.FromSeconds(3));
        foreach(var cert in _owned)cert.Dispose();_stop.Dispose();
    }
}
