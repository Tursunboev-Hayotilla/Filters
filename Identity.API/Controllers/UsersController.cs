﻿using Identity.API.Filters;
using Identity.Application.Abstractions.IServices;
using Identity.Domain.Entities.DTOs;
using Identity.Domain.Entities.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly IAuthService _authService;

        public UsersController(UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager, SignInManager<AppUser> signInManager, IAuthService authService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _signInManager = signInManager;
            _authService = authService;
        }

        [HttpPost("Registration")]
        [AllowAnonymous]
        [ExceptionFilter]
        public async Task<ActionResult<string>> Registration(RegistrationDTO registrationDTO)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (registrationDTO.Password != registrationDTO.ConfirmPassword)
                throw new Exception("Passwords do not match!");

            var user = await _userManager.FindByEmailAsync(registrationDTO.Email);

            if (user is not null)
                throw new Exception("You are already registered");

            var newUser = new AppUser
            {
                FullName = registrationDTO.FullName,
                UserName = registrationDTO.UserName,
                Email = registrationDTO.Email,
                Age = registrationDTO.Age,
            };

            var result = await _userManager.CreateAsync(newUser, registrationDTO.Password);

            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }

            foreach (var role in registrationDTO.Roles)
            {
                await _userManager.AddToRoleAsync(newUser, role);
            }

            var signIn =
                await _signInManager.PasswordSignInAsync(newUser, registrationDTO.Password, false, false);

            if (!signIn.Succeeded)
                throw new Exception("There is an issue with signing in");

            return Ok(result);
        }

        [HttpPost("Login")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResponseDTO>> Login(LoginDTO loginDTO)
        {
            var user = await _userManager.FindByEmailAsync(loginDTO.Email);

            if (user is null)
            {
                return Unauthorized("User not found with this email");
            }

            var test = await _userManager.CheckPasswordAsync(user, loginDTO.Password);

            if (!test)
            {
                return Unauthorized("Password is invalid");
            }

            var result = await _signInManager.PasswordSignInAsync(user, loginDTO.Password, false, false);

            if (!result.Succeeded)
                throw new Exception("There is an issue with signing in");

            var token = await _authService.GenerateToken(user);

            return Ok(token);
        }

        [HttpGet]
        [AuthorizationFilter]
        [ResourceFilter]
        [EndpointFilter]
        public async Task<ActionResult<string>> GetAllUsers()
        {
            var result = await _userManager.Users.ToListAsync();

            return Ok(result);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Admin")]
        [ActionFilter]
        public async Task<IActionResult> GetById(string id)
        {
            try
            {
                var result = await _userManager.Users.FirstOrDefaultAsync(x => x.Id == id);
                if (result is null)
                {
                    return NotFound("User not found");
                }
                return Ok(result);
            }
            catch
            {
                return NotFound("User not found");
            }
        }
    }
}
