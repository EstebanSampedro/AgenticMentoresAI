using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Mentor;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Students;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.Common;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.OutPut;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WebApiTemplate.AuthorizeDLL;

namespace Academikus.AgenteInteligenteMentoresWebApi.WebApi.Controllers;

[Route("api")]
[ApiController]
// [UdlaBasicDbAuthorize]
public class TestController : BaseController<TestController>
{
    protected readonly WebApiBO _webApiBO;
    protected readonly AppSetting _appSetting;

    private readonly IMentorService _mentorService;
    private readonly IStudentService _studentService;

    public TestController(
        WebApiBO webApiBO,
        IOptions<AppSetting> appSetting,
        IMentorService mentorService,
        IStudentService studentService
        )
        : base(webApiBO, appSetting)
    {
        _webApiBO = webApiBO;
        _appSetting = appSetting.Value;

        _mentorService = mentorService;
        _studentService = studentService;
    }

    [HttpGet("test")]
    public async Task<IActionResult> IndexAsync()
    {
        WebApiResponseDTO response = new WebApiResponseDTO();

        GeneralSalesforceUserResponse mentor = new GeneralSalesforceUserResponse();
        //GeneralSalesforceUserResponse student = new GeneralSalesforceUserResponse();

        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            mentor = await _mentorService.GetMentorAsync("andrea.erazo.gallardo@udla.edu.ec");
            //student = await _studentService.GetStudentAsync("andrea.erazo.gallardo@udla.edu.ec");

            // RequestValidator();
        }
        catch (Exception ex) 
        {
            Console.WriteLine(ex);
        }

        return Ok(mentor);
        //return Ok(new { mentor, student });
    }

    //private void RequestValidator()
    //{

    //}
}
