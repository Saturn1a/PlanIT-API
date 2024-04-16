using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlanIT.API.Extensions;
using PlanIT.API.Middleware;
using PlanIT.API.Models.DTOs;
using PlanIT.API.Services.Interfaces;

namespace PlanIT.API.Controllers;

// DinnerController - API Controller for middagsplanleggingh�ndtering:
// - Kontrolleren h�ndterer alle foresp�rsler relatert til middagsdata, inkludert registrering,
//   oppdatering, sletting og henting av middagsinformasjon. Den tar imot en instans av IService
//   som en del av konstrukt�ren for � utf�re operasjoner relatert til middager.
//
// Policy:
// - "Bearer": Krever at alle kall til denne kontrolleren er autentisert med et gyldig JWT-token
//   som oppfyller kravene definert i "Bearer" autentiseringspolicy. Dette sikrer at bare
//   autentiserte brukere kan aksessere endepunktene definert i denne kontrolleren.
//
// HandleExceptionFilter:
// - Dette filteret er tilknyttet kontrolleren for � fange og behandle unntak p� en sentralisert m�te.
//
// Foresp�rsler som starter med "api/v1/Dinner" vil bli rutet til metoder definert i denne kontrolleren.


[Authorize(Policy = "Bearer")]
[ApiController]
[Route("api/v1/[controller]")]
[ServiceFilter(typeof(HandleExceptionFilter))]
public class DinnerController : ControllerBase
{
    private readonly ILogger<DinnerController> _logger;
    private readonly IService<DinnerDTO> _dinnerService;

    public DinnerController(ILogger<DinnerController> logger, 
        IService<DinnerDTO> dinnerService)
    {
        _logger = logger;
        _dinnerService = dinnerService;
    }


    // Endepunkt for registrering av ny middag
    // POST /api/v1/Dinner/register
    [HttpPost("register", Name = "AddEvent")]
    public async Task<ActionResult<DinnerDTO>> AddDinnerAsync(DinnerDTO newDinnerDTO)
    {
        // Sjekk om modelltilstanden er gyldig etter modellbinding og validering
        if (!ModelState.IsValid)
        {
            _logger.LogError("Invalid model state in AddDinnerAsync");
            return BadRequest(ModelState);
        }

        // Registrer middagen
        var addedDinner = await _dinnerService.CreateAsync(newDinnerDTO);

        // Sjekk om middagsregistreringen var vellykket
        return addedDinner != null
            ? Ok(addedDinner)
            : BadRequest("Failed to register new dinner");
    }


    // !!!!!! NB! FJERNE ELLER ADMIN RETTIGHETER??? !!!!!!!!!!!!!!!
    //
    [HttpGet(Name = "GetDinners")]
    public async Task<ActionResult<ICollection<DinnerDTO>>> GetDinnersAsync(int pageNr, int pageSize)
    {
        var allDinners = await _dinnerService.GetAllAsync(pageNr, pageSize);

        return allDinners != null
            ? Ok(allDinners)
            : NotFound("No registered dinners found.");
    }



    // Henter middag basert p� dinnerId
    // GET /api/v1/Dinner/1
    [HttpGet("{dinnerId}", Name = "GetDinnerById")]
    public async Task<ActionResult<DinnerDTO>> GetDinnerByIdAsync(int dinnerId)
    {
        // Henter brukerens ID fra HttpContext.Items som ble lagt til av middleware
        var userId = WebAppExtensions.GetValidUserId(HttpContext);

        // Hent middag fra tjenesten, filtrert etter brukerens ID
        var exsistingDinner = await _dinnerService.GetByIdAsync(userId, dinnerId);

        return exsistingDinner != null
            ? Ok(exsistingDinner)
            : NotFound("Dinner not found");
    }


    // Oppdaterer middag basert p� dinnerID.
    // PUT /api/v1/Dinner/4
    [HttpPut("{dinnerId}", Name = "UpdateDinner")]
    public async Task<ActionResult<DinnerDTO>> UpdateDinnerAsync(int dinnerId, DinnerDTO updatedDinnerDTO)
    {
        // Henter brukerens ID fra HttpContext.Items som ble lagt til av middleware
        var userId = WebAppExtensions.GetValidUserId(HttpContext);

        // Pr�ver � oppdatere middag med den nye informasjonen
        var updatedDinnerResult = await _dinnerService.UpdateAsync(userId, dinnerId, updatedDinnerDTO);

        // Returnerer oppdatert middagsdata, eller en feilmelding hvis oppdateringen mislykkes
        return updatedDinnerResult != null
            ? Ok(updatedDinnerResult)
            : NotFound("Unable to update dinner or the dinner does not belong to the user");
    }


    // Sletter en middag basert p� dinnerID
    // DELETE /api/v1/Dinner/2
    [HttpDelete("{dinnerId}", Name = "DeleteDinner")]
    public async Task<ActionResult<DinnerDTO>> DeleteDinnerAsync(int dinnerId)
    {
        // Henter brukerens ID fra HttpContext.Items som ble lagt til av middleware
        var userId = WebAppExtensions.GetValidUserId(HttpContext);

        // Pr�ver � slette middagen
        var deletedDinnerResult = await _dinnerService.DeleteAsync(userId, dinnerId);

        return deletedDinnerResult != null
            ? Ok(deletedDinnerResult)
            : BadRequest("Unable to delete dinner or the dinner does not belong to the user");

    }
}