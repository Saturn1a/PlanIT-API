using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlanIT.API.Extensions;
using PlanIT.API.Middleware;
using PlanIT.API.Models.DTOs;
using PlanIT.API.Services.Interfaces;

namespace PlanIT.API.Controllers;

// TodoController - API Controller for gj�rem�lsh�ndtering:
// - Kontrolleren h�ndterer alle foresp�rsler relatert til gj�rem�l, inkludert registrering,
//   oppdatering, sletting og henting av gj�rem�l. Den tar imot en instans av IService
//   som en del av konstrukt�ren for � utf�re operasjoner relatert til gj�rem�l.
//
// Policy:
// - "Bearer": Krever at alle kall til denne kontrolleren er autentisert med et gyldig JWT-token
//   som oppfyller kravene definert i "Bearer" autentiseringspolicy. Dette sikrer at bare
//   autentiserte brukere kan aksessere endepunktene definert i denne kontrolleren.
//
// HandleExceptionFilter:
// - Dette filteret er tilknyttet kontrolleren for � fange og behandle unntak p� en sentralisert m�te.
//
// Foresp�rsler som starter med "api/v1/Todo" vil bli rutet til metoder definert i denne kontrolleren.


[Authorize(Policy = "Bearer")]
[ApiController]
[Route("api/v1/[controller]")]
[ServiceFilter(typeof(HandleExceptionFilter))]  // Bruker HandleExceptionFilter for � h�ndtere unntak
public class TodoController : ControllerBase
{
    private readonly IService<ToDoDTO> _todoService;
    private readonly ILogger<TodoController> _logger;

    public TodoController(ILogger<TodoController> logger,
        IService<ToDoDTO> todoService)
    {
        _logger = logger;
        _todoService = todoService;
    }


    // Endepunkt for registrering av nytt gj�rem�l
    // POST /api/v1/Todo/register
    [HttpPost("register", Name = "AddToDo")]
    public async Task<ActionResult<ToDoDTO>> AddToDoAsync(ToDoDTO newTodoDto)
    {
        // Sjekk om modelltilstanden er gyldig etter modellbinding og validering
        if (!ModelState.IsValid)
        {
            _logger.LogError("Invalid model state in AddToDoAsync");
            return BadRequest(ModelState);
        }

        // Registrer gj�rem�let
        var addedToDo = await _todoService.CreateAsync(newTodoDto);

        // Sjekk om registreringen var vellykket
        return addedToDo != null
            ? Ok(addedToDo) 
            : BadRequest("Failed to register new todo item");   
    }


    // !!!!!! NB! FJERNE ELLER ADMIN RETTIGHETER??? !!!!!!!!!!!!!!!
    //
    // Henter en liste over gj�rem�l
    // GET: /api/v1/Todo?pageNr=1&pageSize=10
    [HttpGet( Name = "GetToDoLists")]
    public async Task<ActionResult<IEnumerable<ToDoDTO>>> GetTodosAsync(int pageNr, int pageSize)
    {
        var allToDos = await _todoService.GetAllAsync(pageNr, pageSize);

        return allToDos != null
           ? Ok(allToDos)
           : NotFound("No registered todo items found.");
    }


    // Henter gj�rem�l basert p� toDoId
    // GET /api/v1/Todo/1
    [HttpGet("{toDoId}", Name = "GetToDoById")]
    public async Task<ActionResult<ToDoDTO>> GetTodoByIdAsync(int toDoId)
    {
        // Henter brukerens ID fra HttpContext.Items som ble lagt til av middleware
        var userId = WebAppExtensions.GetValidUserId(HttpContext);

        // Henter gj�rem�l fra tjenesten, filtrert etter brukerens ID
        var exsistingToDo = await _todoService.GetByIdAsync(userId, toDoId);

        return exsistingToDo != null
            ? Ok(exsistingToDo)
            : NotFound("Todo item not found");
    }


    // Oppdaterer gj�rem�l basert p� toDoID.
    // PUT /api/v1/Todo/4
    [HttpPut("{toDoId}", Name = "UpdateTodo")]
    public async Task<ActionResult<ToDoDTO>> UpdateTodoAsync(int toDoId, ToDoDTO updatedTodoDto)
    {
        // Henter brukerens ID fra HttpContext.Items som ble lagt til av middleware
        var userId = WebAppExtensions.GetValidUserId(HttpContext);

        // Pr�ver � oppdatere gj�rem�let med den nye informasjonen
        var updatedToDoResult = await _todoService.UpdateAsync(userId, toDoId, updatedTodoDto);

        // Returnerer oppdatert gj�rem�lsdata, eller en feilmelding hvis oppdateringen mislykkes
        return updatedToDoResult != null
            ? Ok(updatedToDoResult)
            : NotFound("Unable to update the todo item or the todo item does not belong to the user");
    }


    // Sletter et gj�rem�l basert p� toDoID
    // DELETE /api/v1/Events/2
    [HttpDelete("{toDoId}", Name = "DeleteTodo")]
    public async Task<ActionResult<ToDoDTO>> DeleteTodoAsync(int toDoId)
    {
        // Henter brukerens ID fra HttpContext.Items som ble lagt til av middleware
        var userId = WebAppExtensions.GetValidUserId(HttpContext);

        // Pr�ver � slette gj�rem�let
        var deletedToDoResult = await _todoService.DeleteAsync(userId, toDoId);

        return deletedToDoResult != null
            ? Ok(deletedToDoResult)
            : BadRequest("Unable to delete todo item or the todo item does not belong to the user");
    }
}
