using MealApp.Data;
using MealApp.DTOs;
using MealApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MealApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UnitController : ApiControllerBase
{
    private readonly ApplicationDbContext _context;

    public UnitController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var units = await _context.Units
            .Select(unit => new UnitResponseDto
            {
                Id = unit.Id,
                Name = unit.Name,
                ShortName = unit.ShortName
            })
            .ToListAsync();

        return Ok(units);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var unit = await _context.Units.FindAsync(id);

        if (unit is null)
        {
            return NotFound(new
            {
                Message = "Unit not found."
            });
        }

        return Ok(ToResponseDto(unit));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(CreateUnitDto createUnitDto)
    {
        var unit = new Unit
        {
            Name = createUnitDto.Name,
            ShortName = createUnitDto.ShortName
        };

        _context.Units.Add(unit);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = unit.Id }, ToResponseDto(unit));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, UpdateUnitDto updateUnitDto)
    {
        var unit = await _context.Units.FindAsync(id);

        if (unit is null)
        {
            return NotFound(new
            {
                Message = "Unit not found."
            });
        }

        unit.Name = updateUnitDto.Name;
        unit.ShortName = updateUnitDto.ShortName;

        await _context.SaveChangesAsync();

        return Ok(ToResponseDto(unit));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var unit = await _context.Units.FindAsync(id);

        if (unit is null)
        {
            return NotFound(new
            {
                Message = "Unit not found."
            });
        }

        _context.Units.Remove(unit);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            Message = "Unit deleted successfully."
        });
    }

    private static UnitResponseDto ToResponseDto(Unit unit)
    {
        return new UnitResponseDto
        {
            Id = unit.Id,
            Name = unit.Name,
            ShortName = unit.ShortName
        };
    }
}
