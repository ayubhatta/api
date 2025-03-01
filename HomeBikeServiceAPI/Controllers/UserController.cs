using HomeBikeServiceAPI.Models;
using HomeBikeServiceAPI.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using HomeBikeServiceAPI.DTO;
using Microsoft.EntityFrameworkCore;
using HomeBikeServiceAPI.Data;
using Microsoft.AspNetCore.Authorization;
using HomeBikeServiceAPI.Services;


namespace HomeBikeServiceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly IBookingRepo _bookingRepo;
        private readonly CartService _cartService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UserController> _logger;
        private readonly AppDbContext _context;
        private readonly string _jwtSecretKey;
        private readonly string _jwtIssuer;
        private readonly string _jwtAudience;

        public UserController(IUserRepository userRepository, IBookingRepo bookingRepo, CartService cartService,IConfiguration configuration, ILogger<UserController> logger, AppDbContext context)
        {
            _userRepository = userRepository;
            _bookingRepo = bookingRepo;
            _cartService = cartService;
            _configuration = configuration;
            _logger = logger;
            _context = context;
            _jwtSecretKey = configuration["Jwt:SecretKey"];
            _jwtIssuer = configuration["Jwt:Issuer"];
            _jwtAudience = configuration["Jwt:Audience"];
        }

        // GET: api/User/{userId}
        // Restricting this endpoint to Admin role
        [Authorize(Roles = "Admin")]
        [HttpGet("{userId}", Name = "GetUserByIdAsync")]
        public async Task<ActionResult<UserResponseDto>> GetUserByIdAsync(int userId)
        {
            try
            {
                var user = await _userRepository.GetUserByIdAsync(userId);

                if (user == null)
                {
                    return NotFound(new { message = $"User with ID {userId} not found" });
                }

                var userDto = new UserResponseDto
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    PhoneNumber = user.PhoneNumber,
                    Email = user.Email,
                    IsAdmin = user.IsAdmin,
                    Role = user.Role // Corrected Enum conversion
                };

                return Ok(userDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching user by ID.");
                return StatusCode(500, new { message = "Internal server error while fetching user." });
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<ActionResult<List<UserResponseDto>>> GetAllUsersAsync()
        {
            try
            {
                var users = await _userRepository.GetAllUsersAsync();

                if (users == null || users.Count == 0)
                {
                    return NotFound(new { message = "No users found" });
                }

                // Filter users with Role = UserType.User
                var userDtos = users
                    .Where(user => user.Role == UserType.User) // Corrected Enum comparison
                    .Select(user => new UserResponseDto
                    {
                        Id = user.Id,
                        FullName = user.FullName,
                        PhoneNumber = user.PhoneNumber,
                        Email = user.Email,
                        IsAdmin = user.IsAdmin,
                        Role = user.Role 
                    }).ToList();

                if (userDtos.Count == 0)
                {
                    return NotFound(new { message = "No users with role 'User' found." });
                }

                return Ok(userDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching users with role 'User'.");
                return StatusCode(500, new { message = "Internal server error while fetching users." });
            }
        }


        [HttpPost("Register")]
        public async Task<IActionResult> CreateUserAsync(CreateUserRequest createUserRequest)
        {
            if (createUserRequest == null)
            {
                return BadRequest(new { success = false, message = "Invalid user data" });
            }

            try
            {
                var existingUser = await _userRepository.GetUserByEmailAsync(createUserRequest.Email);
                if (existingUser != null)
                {
                    return Conflict(new { success = false, message = "User with this email already exists" });
                }

                bool isAdmin = createUserRequest.Email.Equals("admin@gmail.com", StringComparison.OrdinalIgnoreCase);

                var user = new User
                {
                    FullName = createUserRequest.FullName,
                    PhoneNumber = createUserRequest.PhoneNumber,
                    Email = createUserRequest.Email,
                    Password = createUserRequest.Password,
                    IsAdmin = isAdmin,
                    Role = isAdmin ? UserType.Admin : UserType.User
                };

                var createdUser = await _userRepository.CreateUserAsync(user);

                if (createdUser != null)
                {
                    _logger.LogInformation("Successfully created user with ID: {UserId}", createdUser.Id);

                    var response = new
                    {
                        success = true,
                        message = "User registered successfully.",
                        user = new
                        {
                            id = createdUser.Id,
                            fullName = createdUser.FullName,
                            phoneNumber = createdUser.PhoneNumber,
                            email = createdUser.Email,
                            isAdmin = createdUser.IsAdmin,
                            userRole = createdUser.Role
                        }
                    };

                    return StatusCode(201, response);
                }
                else
                {
                    return StatusCode(500, new { success = false, message = "Failed to create user" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating the user.");
                return StatusCode(500, new { success = false, message = "Internal server error while creating user." });
            }
        }



        private bool VerifyPassword(string storedPassword, string enteredPassword)
        {
            try
            {
                // Split the stored password into salt and hash
                var parts = storedPassword.Split(':');
                if (parts.Length != 2) return false;

                string saltBase64 = parts[0];
                string storedHashBase64 = parts[1];

                // Convert salt from Base64 string back to byte array
                byte[] salt = Convert.FromBase64String(saltBase64);

                // Hash the entered password using the same salt
                byte[] enteredHash = KeyDerivation.Pbkdf2(
                    password: enteredPassword,
                    salt: salt,
                    prf: KeyDerivationPrf.HMACSHA256,
                    iterationCount: 10000,
                    numBytesRequested: 256 / 8);

                string enteredHashBase64 = Convert.ToBase64String(enteredHash);

                // Compare the stored hash with the hash of the entered password
                return storedHashBase64 == enteredHashBase64;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while verifying the password.");
                return false;
            }
        }



        [HttpPost("login")]
        public async Task<IActionResult> LoginUserAsync(LoginRequest loginRequest)
        {
            if (loginRequest == null)
            {
                _logger.LogWarning("Login attempt with null data.");
                return BadRequest(new { message = "Invalid login data. Please provide email and password." });
            }

            try
            {
                if (string.IsNullOrEmpty(loginRequest.Email) || string.IsNullOrEmpty(loginRequest.Password))
                {
                    _logger.LogWarning("Login attempt with empty email or password.");
                    return BadRequest(new { message = "Email and password are required." });
                }

                // Retrieve the user from the database
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == loginRequest.Email);

                if (user == null)
                {
                    _logger.LogWarning("Login failed: User with email {Email} not found.", loginRequest.Email);
                    return Unauthorized(new { message = "Invalid email or password." });
                }

                // Verify the password
                if (!VerifyPassword(user.Password, loginRequest.Password))
                {
                    _logger.LogWarning("Login failed: Incorrect password for user with email {Email}.", loginRequest.Email);
                    return Unauthorized(new { message = "Invalid email or password." });
                }

                // Generate JWT token
                var token = GenerateJwtToken(user);

                _logger.LogInformation("User {Email} logged in successfully.", loginRequest.Email);
                return Ok(new
                {
                    message = "Login successful!",
                    Token = token,
                    user = new
                    {
                        id = user.Id,
                        fullName = user.FullName,
                        phoneNumber = user.PhoneNumber,
                        email = user.Email,
                        isAdmin = user.IsAdmin,
                        role = user.Role.ToString()
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during login attempt.");
                return StatusCode(500, new { message = "Internal server error during login." });
            }
        }

        private string GenerateJwtToken(User user)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_jwtSecretKey);
                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.FullName),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, user.IsAdmin ? "Admin" : "User")  // Add the role claim
                };

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(claims),
                    Expires = DateTime.UtcNow.AddMinutes(30),
                    Issuer = _jwtIssuer,
                    Audience = _jwtAudience,
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
                };

                var token = tokenHandler.CreateToken(tokenDescriptor);
                var tokenString = tokenHandler.WriteToken(token);

                return tokenString;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while generating JWT token.");
                throw new InvalidOperationException("Error while generating JWT token.");
            }
        }


        [Authorize]
        // POST: api/User/forgot-password
        [HttpPost("forgot-password")]
        public async Task<ActionResult> ForgotPasswordAsync(ForgotPasswordRequest request)
        {
            try
            {
                var user = await _userRepository.GetUserByPhoneAsync(request.Phone);
                if (user == null)
                {
                    return NotFound(new { message = $"User with phone {request.Phone} not found" });
                }

                var otp = GenerateOtp();
                user.ResetPasswordOTP = otp;
                user.ResetPasswordOTPExpiry = DateTime.Now.AddMinutes(2);
                await _userRepository.UpdateUserAsync(user);

                // Send OTP to the user's phone or email here
                return Ok(new { message = "OTP sent to your phone" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while sending OTP.");
                return StatusCode(500, new { message = "Internal server error while sending OTP." });
            }
        }


        [HttpPut("UpdateUserRoleToMechanic/{id}")]
        public async Task<IActionResult> UpdateUserRoleToMechanic(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            if (user.Role != UserType.User)
            {
                return BadRequest(new { message = "Only users with the 'User' role can be updated to 'Mechanic'." });
            }

            // Update the role to Mechanic
            user.Role = UserType.Mechanic;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            // Add the user to the Mechanic table
            var mechanic = new Mechanic
            {
                Name = user.FullName,
                PhoneNumber = user.PhoneNumber,
                IsAssignedTo = null
            };

            _context.Mechanics.Add(mechanic);
            await _context.SaveChangesAsync();

            // Return the response with only the required fields
            var response = new
            {
                id = mechanic.Id,
                fullName = mechanic.Name,
                phoneNumber = mechanic.PhoneNumber,
                email = user.Email,
                isAdmin = user.IsAdmin,
                role = (int)user.Role  // Assuming 'Mechanic' is represented by 2 in your enum
            };

            return Ok(response);
        }


        [HttpDelete("{userId}")]
        public async Task<IActionResult> DeleteUser(int userId)
        {
            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { success = false, message = "User not found." });
            }

            try
            {
                // Delete all carts related to the user
                await _cartService.DeleteAllCartsByUserId(userId);

                // Delete all bookings related to the user
                var bookings = await _bookingRepo.GetAllAsync(userId); // Fetch bookings for the user
                if (bookings.Any())
                {
                    _bookingRepo.DeleteRangeAsync(bookings); // Delete the bookings
                    await _bookingRepo.SaveAsync(); // Save changes
                }

                // Delete the user
                _userRepository.DeleteUser(user);
                bool isDeleted = await _userRepository.SaveChangesAsync();

                if (isDeleted)
                {
                    return Ok(new { success = true, message = "User and related data deleted successfully." });
                }
                else
                {
                    return BadRequest(new { success = false, message = "Failed to delete user." });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Internal server error: {ex.Message}" });
            }
        }





        [Authorize]
        // POST: api/User/reset-password
        [HttpPost("reset-password")]
        public async Task<ActionResult> ResetPasswordAsync(ResetPasswordRequest request)
        {
            try
            {
                var user = await _userRepository.GetUserByPhoneAsync(request.Phone);
                if (user == null || user.ResetPasswordOTP != request.Otp || user.ResetPasswordOTPExpiry < DateTime.Now)
                {
                    return BadRequest(new { message = "Invalid OTP or OTP expired" });
                }

                user.Password = request.NewPassword;
                user.ResetPasswordOTP = null;
                user.ResetPasswordOTPExpiry = null;
                await _userRepository.UpdateUserAsync(user);

                return Ok(new { message = "Password reset successful" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while resetting the password.");
                return StatusCode(500, new { message = "Internal server error while resetting password." });
            }
        }

        // Helper method to generate OTP
        private string GenerateOtp()
        {
            Random random = new Random();
            return random.Next(100000, 999999).ToString();
        }
    }
}
