using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using HomeBikeServiceAPI.Controllers;
using HomeBikeServiceAPI.Data;
using Microsoft.AspNetCore.Builder;
using HomeBikeServiceAPI.Repositories;
using HomeBikeServiceAPI.Interfaces;
using HomeBikeServiceAPI.Services;
using HomeBikeServiceAPI.BackgroundServices;

public static class HomeServicesStartup
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IUserRepository, UserRepository>();
        services.AddTransient<IBookingRepo, BookingRepo>();
        services.AddTransient<IBikePartsRepository, BikePartsRepository>();
        services.AddTransient<ICartRepository, CartRepository>();
        services.AddTransient<BikePartsService>();
        services.AddTransient<CartService>();
        services.AddTransient<IFeedbackRepo, FeedbackRepo>();
        services.AddTransient<IMechanicRepository, MechanicRepository>();
        services.AddTransient<IEmailService, EmailService>();
        services.AddTransient<JobTriggerService>();
        services.AddTransient<JobService>();
        services.AddTransient<TotalSumController>();
    }
}

