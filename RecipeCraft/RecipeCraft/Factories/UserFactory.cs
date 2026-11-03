using Microsoft.AspNetCore.Identity;
using RecipeCraft.Models;

namespace RecipeCraft.Factories
{
    /// <summary>
    /// Интерфейс за Factory Pattern, отговорен за капсулирането на логиката 
    /// по инстанциране и първоначална настройка на Потребители (User objects).
    /// </summary>
    public interface IUserFactory
    {
        /// <summary>
        /// Създава нов потребителски обект с автоматично хеширана парола и роля по подразбиране.
        /// </summary>
        /// <param name="username">Потребителско име</param>
        /// <param name="email">Имейл адрес</param>
        /// <param name="firstName">Собствено име</param>
        /// <param name="lastName">Фамилно име</param>
        /// <param name="rawPassword">Нехеширана (сурова) парола</param>
        /// <param name="role">Роля на потребителя в системата (по подразбиране: "User")</param>
        /// <returns>Конструиран и подготвен за запис обект от тип <see cref="User"/></returns>
        User CreateUser(string username, string email, string firstName, string lastName, string rawPassword, string role = "User");
    }

    /// <summary>
    /// Конкретна реализация на Factory Pattern (Фабрика за потребители).
    /// Централизира създаването на потребители и премахва дублирането на логиката за хеширане на пароли.
    /// </summary>
    public class UserFactory : IUserFactory
    {
        public User CreateUser(string username, string email, string firstName, string lastName, string rawPassword, string role = "User")
        {
            // 1. Инициализиране на новия потребителски обект
            var user = new User
            {
                Username = username,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                Role = string.IsNullOrWhiteSpace(role) ? "User" : role
            };

            // 2. Сигурно хеширане на паролата чрез ASP.NET Core PasswordHasher
            var hasher = new PasswordHasher<User>();
            user.PasswordHash = hasher.HashPassword(user, rawPassword);

            // 3. Връщане на готовия за запис в базата данни обект
            return user;
        }
    }
}