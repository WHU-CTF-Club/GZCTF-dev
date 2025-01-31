using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Flurl;
using Flurl.Http;
using Microsoft.Extensions.Options;
using GZCTF.Models.Internal;
using Newtonsoft.Json.Linq;

namespace GZCTF.Services.Proxy;

public class ProxyRequest
{
    readonly ILogger<ProxyRequest> _logger;
    readonly ProxyConfig? _options;

    public ProxyRequest(
        IOptions<ProxyConfig> options,
        ILogger<ProxyRequest> logger)
    {
        _logger = logger;
        _options = options.Value;
    }

    public async Task<bool> CreateProxyRequestAsync(Models.Data.Container container)
    {
        try
        {
            if (_options == null || _options.Url == "")
            {
                return false;
            }

            int? port = container.PublicPort;
            if (port == null)
            {
                return false;
            }

            string name = Guid.NewGuid().ToString();

            while (true)
            {
                JsonElement response = await $"{_options.Url}/config/services"
                    .WithHeader("Authorization", $"Basic {_options.Auth}")
                    .PostJsonAsync(new
                        {
                            addr = $":{port}",
                            handler = new { type = "tcp" },
                            listener = new { type = "tcp" },
                            forwarder = new { nodes = new[] { new { addr = $"{_options.LocalIP}:{container.PublicPort}" } } },
                            name = name
                        }
                    ).ReceiveJson<JsonElement>();

                
                if (response.GetProperty("msg").GetString() == "OK")
                {
                    container.proxyServiceName = name;
                    container.proxyServicePort = port;
                    return true;
                }
                
                switch (response.GetProperty("code").GetInt32())
                {
                    case 40002: // service name already exists
                        name = Guid.NewGuid().ToString();
                        break;
                    case 40003: // port already in use
                        port += 1;
                        break;
                    default:    // unknown error
                        return false;
                }
                if (port > 65535)
                {
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            return false;
        }
    }

    public async Task<bool> DeleteProxyRequestAsync(Models.Data.Container container)
    {
        try
        {
            if (_options == null || _options.Url == "")
            {
                return false;
            }
            
            JsonElement response = await $"{_options.Url}/config/services/{container.proxyServiceName}"
                .WithHeader("Authorization", $"Basic {_options.Auth}")
                .DeleteAsync()
                .ReceiveJson<JsonElement>();
            if (response.GetProperty("msg").GetString() == "OK")
            {
                return true;
            }
            switch (response.GetProperty("code").GetInt32() )
            {
                default:    // unknown error
                    return false;
            }
        }
        catch (FlurlHttpException ex)
        {
            _logger.LogError(ex.Message);
            return false;
        }
    }
}