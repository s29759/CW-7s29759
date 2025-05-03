using Microsoft.AspNetCore.Mvc;
using ProjektWebAPI.Services;

namespace ProjektWebAPI.Controllers;


[ApiController]
[Route("[controller]")]
public class TripsController(IDbService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAllTrips()
    {
        return Ok(await service.GetTripsAsync());
    }
    
}