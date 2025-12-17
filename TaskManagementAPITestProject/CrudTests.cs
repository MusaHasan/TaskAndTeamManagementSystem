using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskMansagement.Controllers;
using TaskMansagement.Data;
using TaskMansagement.Models;
using FluentAssertions;
using Xunit;
using TaskStatus = TaskMansagement.Models.TaskStatus;

namespace TaskManagementAPITestProject
{
    public class CrudTests
    {
        private AppDbContext CreateContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;

            return new AppDbContext(options);
        }

        private ClaimsPrincipal CreatePrincipal(Guid userId, Role role)
        {
            var claims = new[] {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role.ToString())
            };
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        }

        private void AttachUserToController(ControllerBase controller, ClaimsPrincipal principal)
        {
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };
        }

        [Fact]
        public async Task Users_CRUD_Works()
        {
            using var db = CreateContext("Users_CRUD");

            var usersController = new UsersController(db);

            var user = new User
            {
                FullName = "Test User",
                Email = "testuser@local",
                Role = Role.Employee,
                PasswordHash = "hash"
            };

            var createResult = await usersController.Create(user) as CreatedAtActionResult;
            createResult.Should().NotBeNull();

            var created = createResult.Value as User;
            created.Should().NotBeNull();
            created.Id.Should().NotBeEmpty();

            var allResult = await usersController.GetAll() as OkObjectResult;
            allResult.Should().NotBeNull();
            var list = allResult.Value as System.Collections.Generic.List<User>;
            list.Should().ContainSingle(u => u.Id == created.Id);

            // Update
            var updated = new User { FullName = "Updated", Email = "updated@local", Role = Role.Manage };
            var updateResult = await usersController.Update(created.Id, updated) as NoContentResult;
            updateResult.Should().NotBeNull();

            var fetched = await db.Users.FindAsync(created.Id);
            fetched!.FullName.Should().Be("Updated");

            // Delete
            var deleteResult = await usersController.Delete(created.Id) as NoContentResult;
            deleteResult.Should().NotBeNull();

            var after = await db.Users.FindAsync(created.Id);
            after.Should().BeNull();
        }

        [Fact]
        public async Task Teams_CRUD_Works()
        {
            using var db = CreateContext("Teams_CRUD");

            var teamsController = new TeamsController(db);

            var team = new Team { Name = "Alpha", Description = "Team Alpha" };
            var createResult = await teamsController.Create(team) as CreatedAtActionResult;
            createResult.Should().NotBeNull();

            var created = createResult.Value as Team;
            created.Should().NotBeNull();

            var allResult = await teamsController.GetAll() as OkObjectResult;
            allResult.Should().NotBeNull();
            var list = allResult.Value as System.Collections.Generic.List<Team>;
            list.Should().ContainSingle(t => t.Id == created.Id);

            // Update
            var updated = new Team { Name = "Beta", Description = "Team Beta" };
            var updateResult = await teamsController.Update(created.Id, updated) as NoContentResult;
            updateResult.Should().NotBeNull();

            var fetched = await db.Teams.FindAsync(created.Id);
            fetched!.Name.Should().Be("Beta");

            // Delete
            var deleteResult = await teamsController.Delete(created.Id) as NoContentResult;
            deleteResult.Should().NotBeNull();

            var after = await db.Teams.FindAsync(created.Id);
            after.Should().BeNull();
        }

        [Fact]
        public async Task Tasks_CRUD_Role_Behavior()
        {
            using var db = CreateContext("Tasks_CRUD_Role");

            // create manager and employee users in db
            var manager = new User { Id = Guid.NewGuid(), FullName = "Mng", Email = "m@local", Role = Role.Manage, PasswordHash = "h" };
            var employee = new User { Id = Guid.NewGuid(), FullName = "Emp", Email = "e@local", Role = Role.Employee, PasswordHash = "h" };
            db.Users.Add(manager);
            db.Users.Add(employee);
            await db.SaveChangesAsync();

            var tasksController = new TasksController(db);

            // Manager can create
            var managerPrincipal = CreatePrincipal(manager.Id, Role.Manage);
            AttachUserToController(tasksController, managerPrincipal);

            var task = new TaskItem { Title = "Work", Description = "Do work", Status = TaskStatus.Todo, AssignedToUserId = employee.Id, CreatedByUserId = manager.Id, TeamId = Guid.Empty };
            var createResult = await tasksController.Create(task) as CreatedAtActionResult;
            createResult.Should().NotBeNull();
            var created = createResult.Value as TaskItem;
            created.Should().NotBeNull();

            // Employee cannot create (should get Forbid)
            var tasksController2 = new TasksController(db);
            var empPrincipal = CreatePrincipal(employee.Id, Role.Employee);
            AttachUserToController(tasksController2, empPrincipal);

            var task2 = new TaskItem { Title = "EmpTask", Description = "Nope", Status = TaskStatus.Todo, AssignedToUserId = employee.Id, CreatedByUserId = employee.Id, TeamId = Guid.Empty };
            var createResult2 = await tasksController2.Create(task2);
            createResult2.Should().BeOfType<ForbidResult>();

            // Employee can update status of their assigned task
            var tasksController3 = new TasksController(db);
            AttachUserToController(tasksController3, empPrincipal);

            var updatedStatus = new TaskItem { Status = TaskStatus.InProgress };
            var updateResult = await tasksController3.Update(created.Id, updatedStatus) as NoContentResult;
            updateResult.Should().NotBeNull();

            var fetched = await db.Tasks.FindAsync(created.Id);
            fetched!.Status.Should().Be(TaskStatus.InProgress);

            // Manager can update any task details
            var tasksController4 = new TasksController(db);
            AttachUserToController(tasksController4, managerPrincipal);

            var updatedAll = new TaskItem { Title = "UpdatedTitle", Description = "UpdatedDesc", Status = TaskStatus.Done, AssignedToUserId = employee.Id, CreatedByUserId = manager.Id, TeamId = Guid.Empty };
            var updateResult2 = await tasksController4.Update(created.Id, updatedAll) as NoContentResult;
            updateResult2.Should().NotBeNull();

            var fetched2 = await db.Tasks.FindAsync(created.Id);
            fetched2!.Title.Should().Be("UpdatedTitle");
            fetched2.Status.Should().Be(TaskStatus.Done);
        }
    }
}
