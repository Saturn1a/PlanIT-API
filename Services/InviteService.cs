﻿using PlanIT.API.Mappers.Interface;
using PlanIT.API.Models.DTOs;
using PlanIT.API.Models.Entities;
using PlanIT.API.Repositories.Interfaces;
using PlanIT.API.Services.Interfaces;
using PlanIT.API.Utilities; // For å inkludere LoggerService og ExceptionHelper


namespace PlanIT.API.Services;

// Serviceklasse for håndtering av invitasjonsinformasjon.
// Exceptions blir fanget av en middleware: HandleExceptionFilter
public class InviteService : IService<InviteDTO>
{
    private readonly IMapper<Invite, InviteDTO> _inviteMapper;
    private readonly IRepository<Invite> _inviteRepository;
    private readonly IRepository<Event> _eventRepository;
    private readonly LoggerService _logger;
    private readonly IMailService _mailService;

    public InviteService(IMapper<Invite, InviteDTO> inviteMapper,
        IRepository<Invite> inviteRepository,
        IRepository<Event> eventRepository,
        LoggerService logger,
        IMailService mailService)
    {
        _inviteMapper = inviteMapper;
        _inviteRepository = inviteRepository;
        _eventRepository = eventRepository;
        _logger = logger;
        _mailService = mailService;
    }

    // Oppretter ny invitasjon basert på data mottatt fra klienten
    public async Task<InviteDTO?> CreateAsync(InviteDTO newInviteDTO)
    {
        _logger.LogCreationStart("invite");
        var newInvite = _inviteMapper.MapToModel(newInviteDTO);

        // Legger til den nye invitasjonen i databasen og henter resultatet
        var addedInvite = await _inviteRepository.AddAsync(newInvite);
        if (addedInvite == null)
        {
            _logger.LogCreationFailure("invite");
            throw ExceptionHelper.CreateOperationException("invite", 0, "create");
        }

        //_mailService.SendInviteEmail(addedInvite);

        _logger.LogOperationSuccess("created", "invite", addedInvite.Id);
        return _inviteMapper.MapToDTO(addedInvite);
    }


    // *********NB!! FJERNE??? *************
    //
    // Henter alle invitasjoner
    public async Task<ICollection<InviteDTO>> GetAllAsync(int pageNr, int pageSize)
    {
        // Henter invitasjonsinformasjon fra repository med paginering
        var invitesFromRepository = await _inviteRepository.GetAllAsync(1, 10);

        // Mapper invitasjonsdataene til inviteDTO-format
        var inviteDTOs = invitesFromRepository.Select(inviteEntity => _inviteMapper.MapToDTO(inviteEntity)).ToList();
        return inviteDTOs;
    }


    // Henter en invitasjon basert på dens ID og brukerens ID for å sikre at brukeren har tilgang
    public async Task<InviteDTO?> GetByIdAsync(int userIdFromToken, int inviteId)
    {
        _logger.LogDebug("Attempting to retrieve invite with ID {InviteId} for user ID {UserId}.", inviteId, userIdFromToken);
        var invite = await _inviteRepository.GetByIdAsync(inviteId);
        if (invite == null)
        {
            _logger.LogNotFound("invite", inviteId);
            throw ExceptionHelper.CreateNotFoundException("invite", inviteId);
        }

        // Henter det assosierte arrangementet for å kunne sjekke mot riktig brukerID
        var eventAssociated = await _eventRepository.GetByIdAsync(invite.EventId);
        if (eventAssociated == null || eventAssociated.UserId != userIdFromToken)
        {
            _logger.LogUnauthorizedAccess("invite", inviteId, userIdFromToken);
            throw ExceptionHelper.CreateUnauthorizedException("invite", inviteId);
        }

        _logger.LogOperationSuccess("retrieved", "invite", inviteId);
        return _inviteMapper.MapToDTO(invite);
    }


    // Oppdaterer en eksisterende invitasjon etter å ha validert at brukeren har nødvendige rettigheter
    public async Task<InviteDTO?> UpdateAsync(int userIdFromToken, int inviteId, InviteDTO inviteDTO)
    {
        _logger.LogDebug("Attempting to update invite with ID {InviteId} for user ID {UserId}.", inviteId, userIdFromToken);

        // Forsøker å hente en invitasjon basert på ID for å sikre at det faktisk eksisterer før oppdatering.
        var existingInvite = await _inviteRepository.GetByIdAsync(inviteId);
        if (existingInvite == null)
        {
            _logger.LogNotFound("invite", inviteId);
            throw ExceptionHelper.CreateNotFoundException("invite", inviteId);
        }

        // Henter det assosierte arrangementet for å kunne sjekke mot riktig brukerID
        var associatedEvent = await _eventRepository.GetByIdAsync(existingInvite.EventId);
        if (associatedEvent == null || associatedEvent.UserId != userIdFromToken)
        {
            _logger.LogUnauthorizedAccess("invite", inviteId, userIdFromToken);
            throw ExceptionHelper.CreateUnauthorizedException("invite", inviteId);
        }

        // Mapper til DTO og sørger for at ID forblir den samme under oppdateringen
        var inviteToUpdate = _inviteMapper.MapToModel(inviteDTO);
        inviteToUpdate.Id = inviteId;

        // Prøver å oppdatere invitasjonen i databasen
        var updatedInvite = await _inviteRepository.UpdateAsync(inviteId, inviteToUpdate);
        if (updatedInvite == null)
        {
            _logger.LogOperationFailure("update", "invite", inviteId);
            throw ExceptionHelper.CreateOperationException("invite", inviteId, "update");
        }

        _logger.LogOperationSuccess("updated", "invite", inviteId);
        return _inviteMapper.MapToDTO(updatedInvite);
    }


    // Sletter en invitasjon etter å ha validert brukerens tilgangsrettigheter
    public async Task<InviteDTO?> DeleteAsync(int userIdFromToken, int inviteId)
    {
        _logger.LogDebug("Attempting to delete invite with ID {InviteId}.", inviteId);

        // Forsøker å hente en invitasjon basert på ID for å sikre at det faktisk eksisterer før sletting.
        var inviteToDelete = await _inviteRepository.GetByIdAsync(inviteId);
        if (inviteToDelete == null)
        {
            _logger.LogNotFound("invite", inviteId);
            throw ExceptionHelper.CreateNotFoundException("invite", inviteId);
        }

        // Henter det assosierte arrangementet for å kunne sjekke mot riktig brukerID
        var associatedEvent = await _eventRepository.GetByIdAsync(inviteToDelete.EventId);
        if (associatedEvent == null || associatedEvent.UserId != userIdFromToken)
        {
            _logger.LogUnauthorizedAccess("invite", inviteId, userIdFromToken);
            throw ExceptionHelper.CreateUnauthorizedException("invite", inviteId);
        }

        // Prøver å slette invitasjonen fra databasen
        var deletedInvite = await _inviteRepository.DeleteAsync(inviteId);
        if (deletedInvite == null)
        {
            _logger.LogOperationFailure("delete", "invite", inviteId);
            throw ExceptionHelper.CreateOperationException("invite", inviteId, "delete");
        }

        _logger.LogOperationSuccess("deleted", "invite", inviteId);
        return _inviteMapper.MapToDTO(deletedInvite);
    }
}