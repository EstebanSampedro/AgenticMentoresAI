using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

public class BannerCodeRequest
{
    [JsonProperty("Codigo_banner__c")]
    public string CodigoBanner { get; set; }

    public BannerCodeRequest(string bannerCode)
    {
        CodigoBanner = bannerCode;
    }
}
