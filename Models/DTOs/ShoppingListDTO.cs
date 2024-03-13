﻿namespace PlanIT.API.Models.DTOs;

public class ShoppingListDTO
{
    public ShoppingListDTO(int id, int userId, string name)
    {
        Id = id;
        UserId = userId;
        Name = name;
    }

    public int Id { get; set; }

    public int UserId { get; set; }

    public string Name { get; set; } = string.Empty;
}
