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
    public class TasksController : ControllerBase
    {
        private readonly AppDbContext _db;

        public TasksController(AppDbContext db)
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
        public async Task<IActionResult> GetAll([FromQuery] TaskMansagement.Models.TaskStatus? status, [FromQuery] Guid? assignedToUserId, [FromQuery] Guid? teamId, [FromQuery] DateTime? dueDate)
        {
            var query = _db.Tasks.AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(t => t.Status == status.Value);
            }

            if (assignedToUserId.HasValue)
            {
                query = query.Where(t => t.AssignedToUserId == assignedToUserId.Value);
            }

            if (teamId.HasValue)
            {
                query = query.Where(t => t.TeamId == teamId.Value);
            }

            if (dueDate.HasValue)
            {
                var date = dueDate.Value.Date;
                query = query.Where(t => t.DueDate.HasValue && t.DueDate.Value.Date == date);
            }

            var tasks = await query.ToListAsync();
            return Ok(tasks);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var task = await _db.Tasks.FindAsync(id);
            if (task == null) return NotFound();
            return Ok(task);
        }

        [HttpPost]
        [Authorize(Policy = "ManagerOrAdmin")]
        public async Task<IActionResult> Create([FromBody] TaskItem task)
        {
            var current = await GetCurrentUser();
            if (current == null) return Unauthorized();

            task.Id = Guid.NewGuid();
            _db.Tasks.Add(task);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = task.Id }, task);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] TaskItem updated)
        {
            var current = await GetCurrentUser();
            if (current == null) return Unauthorized();

            var task = await _db.Tasks.FindAsync(id);
            if (task == null) return NotFound();

            // Admin and Manage can update any task
            if (current.Role == Role.Admin || current.Role == Role.Manage)
            {
                task.Title = updated.Title;
                task.Description = updated.Description;
                task.Status = updated.Status;
                task.AssignedToUserId = updated.AssignedToUserId;
                task.CreatedByUserId = updated.CreatedByUserId;
                task.TeamId = updated.TeamId;
                task.DueDate = updated.DueDate;

                _db.Tasks.Update(task);
                await _db.SaveChangesAsync();
                return NoContent();
            }

            // Employee can only update status of their assigned tasks
            if (current.Role == Role.Employee)
            {
                if (task.AssignedToUserId != current.Id) return Forbid();

                task.Status = updated.Status;
                _db.Tasks.Update(task);
                await _db.SaveChangesAsync();
                return NoContent();
            }

            return Forbid();
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var current = await GetCurrentUser();
            if (current == null) return Unauthorized();
            if (current.Role != Role.Admin) return Forbid();

            var task = await _db.Tasks.FindAsync(id);
            if (task == null) return NotFound();

            _db.Tasks.Remove(task);
            await _db.SaveChangesAsync();

            return NoContent();
        }
    }
}
