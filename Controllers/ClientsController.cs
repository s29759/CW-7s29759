using ProjektWebAPI.Exceptions;
using ProjektWebAPI.Models.DTOs;
using ProjektWebAPI.Services;

namespace ProjektWebAPI.Controllers;

using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;
using ProjektWebAPI.Models;

[ApiController]
[Route("[controller]")]
public class ClientsController(IDbService service) : ControllerBase
{

    [HttpGet("{id}/trips")]
    public async Task<IActionResult> GetClientTrips([FromRoute] int id)
    {
        try
        {
            var trips = await service.GetClientTripsAsync(id);
            return Ok(trips);
        }
        catch (NotFoundException e)
        {
            return NotFound(e.Message);
        }
    }

    [HttpPost("clients")]
    public async Task<IActionResult> AddClient([FromBody] ClientPostDTO dto)
    {
        var newId = await service.AddClientAsync(dto);
        return Created($"clients/{newId}", new {Id = newId});
    }

    [HttpPut("clients/{id}/trips/{tripId}")]
    public async Task<IActionResult> RegisterClientToTrip([FromRoute] int id, [FromRoute] int tripId)
    {
        try
        {
            await service.RegisterClientToTripAsync(id, tripId);
            return Ok("Client successfully registered");
        }
        catch (NotFoundException e)
        {
            return NotFound(e.Message);
        }
        catch (Exception e)
        {
            return BadRequest(e.Message);
        }
    }

    [HttpDelete("clients/{id}/trips/{tripId}")]
    public async Task<IActionResult> DeleteClientFromTrip([FromRoute] int id, [FromRoute] int tripId)
    {
        try
        {
            await service.DeleteClientTripAsync(id, tripId);
            return Ok("Registration for client successfully deleted");
        }
        catch (NotFoundException e)
        {
            return NotFound(e.Message);
        }
    }
    
}