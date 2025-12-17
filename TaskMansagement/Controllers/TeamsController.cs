using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskMansagement.Data;
using TaskMansagement.Models;

namespace TaskMansagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TeamsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public TeamsController(AppDbContext db)
        {
            _db = db;
        }

        private async Task<User?> GetCurrentUser()
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                var idClaim = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier);
                if (idClaim != null && Guid.TryParse(idClaim.Value, out var id))
                {
                    return await _db.Users.FindAsync(id);
                }
            }

            if (!Request.Headers.TryGetValue("X-User-Id", out var userIdVal)) return null;
            if (!Guid.TryParse(userIdVal.FirstOrDefault(), out var userId)) return null;
            return await _db.Users.FindAsync(userId);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll()
        {
            var teams = await _db.Teams.ToListAsync();
            return Ok(teams);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var team = await _db.Teams.FindAsync(id);
            if (team == null) return NotFound();
            return Ok(team);
        }

        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Create([FromBody] Team team)
        {
            team.Id = Guid.NewGuid();
            _db.Teams.Add(team);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = team.Id }, team);
        }

        [HttpPut("{id}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Update(Guid id, [FromBody] Team updated)
        {
            var team = await _db.Teams.FindAsync(id);
            if (team == null) return NotFound();

            team.Name = updated.Name;
            team.Description = updated.Description;

            _db.Teams.Update(team);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var team = await _db.Teams.FindAsync(id);
            if (team == null) return NotFound();

            _db.Teams.Remove(team);
            await _db.SaveChangesAsync();

            return NoContent();
        }
    }
}
