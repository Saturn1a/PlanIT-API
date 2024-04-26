using PlanIT.API.Mappers.Interface;
using PlanIT.API.Models.DTOs;
using PlanIT.API.Models.Entities;
using PlanIT.API.Repositories.Interfaces;
using PlanIT.API.Services.Interfaces;
using PlanIT.API.Utilities;

namespace PlanIT.API.Services;


// Serviceklasse for h�ndtering av middagsinformasjon.
// Exceptions blir fanget av en middleware: HandleExceptionFilter
public class DinnerService : IDinnerService
{
    private readonly IMapper<Dinner, DinnerDTO> _dinnerMapper;
    private readonly IWeeklyDinnerPlanMapper _weeklyDinnerPlanMapper;
    private readonly IDinnerRepository _dinnerRepository;
    private readonly LoggerService _logger;

    public DinnerService(IMapper<Dinner, DinnerDTO> dinnerMapper,
        IWeeklyDinnerPlanMapper weeklyDinnerPlanMapper,
        IDinnerRepository dinnerRepository,
        LoggerService logger)
    {
        _dinnerMapper = dinnerMapper;
        _weeklyDinnerPlanMapper = weeklyDinnerPlanMapper;
        _dinnerRepository = dinnerRepository;
        _logger = logger;
    }


    // Oppretter ny middag
    public async Task<DinnerDTO?> CreateAsync(int userIdFromToken, DinnerDTO dinnerDTO)
    {
        _logger.LogCreationStart("dinner");

        var newDinner = _dinnerMapper.MapToModel(dinnerDTO);
        newDinner.UserId = userIdFromToken;

        // Fors�ker � legge til den nye middagen i databasen
        var addedDinner = await _dinnerRepository.AddAsync(newDinner);
        if (addedDinner == null)
        {
            _logger.LogCreationFailure("dinner");
            throw ExceptionHelper.CreateOperationException("dinner", 0, "create");
        }

        _logger.LogOperationSuccess("created", "dinner", addedDinner.Id);

        return _dinnerMapper.MapToDTO(addedDinner);
    }


    public async Task<bool> RegisterWeeklyDinnerPlanAsync(int userId, WeeklyDinnerPlanDTO weeklyPlanDTO)
    {
        var dinners = weeklyPlanDTO.ToDinnerDTOs().Select(dto => new Dinner
        {
            UserId = userId,
            Date = dto.Date,
            Name = dto.Name
        }).ToList();

        try
        {
            bool result = await _dinnerRepository.AddWeeklyDinnersAsync(dinners);
            if (!result)
            {
                _logger.LogCreationFailure("weekly dinner plan");
                throw ExceptionHelper.CreateOperationException("weekly dinner plan", userId, "register");
            }

            _logger.LogOperationSuccess("registered", "weekly dinner plan", userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "Failed to register weekly dinner plan for user {UserId}.", userId);
            throw; // Propagates the exception, ensuring it can be handled by middleware or further up the call stack
        }
    }


    // Henter alle middager med paginering
    public async Task<ICollection<DinnerDTO>> GetAllAsync(int userIdFromToken,int pageNr, int pageSize)
    {
        // Henter Contacts fra repository med paginering
        var dinnersFromRepository = await _dinnerRepository.GetAllAsync(1, 10);
        
        // filter
        var filteredDinners = dinnersFromRepository.Where(dinner => dinner.UserId == userIdFromToken);

        // Mapper Contactsinformasjon til contactsDTO-format
        return filteredDinners.Select(dinnerEntity => _dinnerMapper.MapToDTO(dinnerEntity)).ToList();


    }


    // Henter en middag basert p� ID
    public async Task<DinnerDTO?> GetByIdAsync(int userIdFromToken, int dinnerId)
    {
        _logger.LogDebug($"Henter middag med ID {dinnerId} for bruker {userIdFromToken}.");

        var dinnerFromRepository = await _dinnerRepository.GetByIdAsync(dinnerId);
        if (dinnerFromRepository == null)
        {
            _logger.LogNotFound("dinner", dinnerId);
            throw ExceptionHelper.CreateNotFoundException("dinner", dinnerId);
        }

        // Sjekker om brukerens ID stemmer overens med brukerID tilknyttet middagen
        if (dinnerFromRepository.UserId != userIdFromToken)
        {
            _logger.LogUnauthorizedAccess("dinner", dinnerId, userIdFromToken);
            throw ExceptionHelper.CreateUnauthorizedException("dinner", dinnerId);
        }

        // Logger vellykket henting av middagen
        _logger.LogOperationSuccess("retrieved", "dinner", dinnerId);
        return _dinnerMapper.MapToDTO(dinnerFromRepository);
    }


    public async Task<WeeklyDinnerPlanDTO> GetWeeklyDinnerPlanAsync(int userIdFromToken, DateOnly startDate, DateOnly endDate)
    {
        _logger.LogDebug("Fetching weekly dinner plan for user {UserId} from {StartDate} to {EndDate}.", userIdFromToken, startDate, endDate);

        // Fetching dinners within the specified date range for the given user
        var dinnersFromRepository = await _dinnerRepository.GetByDateRangeAndUserAsync(userIdFromToken, startDate, endDate);

        if (dinnersFromRepository == null)
        {
            _logger.LogNotFound("dinners", userIdFromToken);
            throw ExceptionHelper.CreateNotFoundException("dinners for user", userIdFromToken);
        }

        _logger.LogOperationSuccess("retrieved", "weekly dinner plan", userIdFromToken);
        return _weeklyDinnerPlanMapper.MapToDTO(dinnersFromRepository);

    }


    // Oppdaterer en middag
    public async Task<DinnerDTO?> UpdateAsync(int userIdFromToken, int dinnerId, DinnerDTO dinnerDTO)
    {
        _logger.LogDebug($"Oppdaterer middag med ID {dinnerId} for bruker {userIdFromToken}.");

        // Fors�ker � hente den eksisterende middagen fra databasen
        var existingDinner = await _dinnerRepository.GetByIdAsync(dinnerId);
        if (existingDinner == null)
        {
            _logger.LogNotFound("dinner", dinnerId);
            throw ExceptionHelper.CreateNotFoundException("dinner", dinnerId);
        }

        // Sjekker om brukeren har autorisasjon til � oppdatere middagen
        if (existingDinner.UserId != userIdFromToken)
        {
            _logger.LogUnauthorizedAccess("dinner", dinnerId, userIdFromToken);
            throw ExceptionHelper.CreateUnauthorizedException("dinner", dinnerId);
        }

        var dinnerToUpdate = _dinnerMapper.MapToModel(dinnerDTO);
        dinnerToUpdate.Id = dinnerId;  // Sikrer at ID ikke endres under oppdateringen

        // Utf�rer oppdateringen i databasen
        var updatedDinner = await _dinnerRepository.UpdateAsync(dinnerId, dinnerToUpdate);
        if (updatedDinner == null)
        {
            _logger.LogOperationFailure("update", "dinner", dinnerId);
            throw ExceptionHelper.CreateOperationException("dinner", dinnerId, "update");
        }

        _logger.LogOperationSuccess("updated", "dinner", dinnerId);
        return _dinnerMapper.MapToDTO(updatedDinner);
    }


    // Sletter en middag
    public async Task<DinnerDTO?> DeleteAsync(int userIdFromToken, int dinnerId)
    {
        _logger.LogDebug($"Fors�ker � slette middag med ID {dinnerId} av bruker {userIdFromToken}.");

        // Henter middagen fra databasen for � sikre at den eksisterer f�r sletting
        var dinnerToDelete = await _dinnerRepository.GetByIdAsync(dinnerId);
        if (dinnerToDelete == null)
        {
            _logger.LogNotFound("dinner", dinnerId);
            throw ExceptionHelper.CreateNotFoundException("dinner", dinnerId);
        }

        // Sjekker om brukeren har riktig autorisasjon til � slette middagen
        if (dinnerToDelete.UserId != userIdFromToken)
        {
            _logger.LogUnauthorizedAccess("dinner", dinnerId, userIdFromToken);
            throw ExceptionHelper.CreateUnauthorizedException("dinner", dinnerId);
        }

        // Utf�rer slettingen av middagen fra databasen
        var deletedDinner = await _dinnerRepository.DeleteAsync(dinnerId);
        if (deletedDinner == null)
        {
            _logger.LogOperationFailure("delete", "dinner", dinnerId);
            throw ExceptionHelper.CreateOperationException("dinner", dinnerId, "delete");
        }

        _logger.LogOperationSuccess("deleted", "dinner", dinnerId);
        return _dinnerMapper.MapToDTO(deletedDinner);
    }
}