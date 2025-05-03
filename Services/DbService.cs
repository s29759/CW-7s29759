using System.Data.SqlClient;
using ProjektWebAPI.Exceptions;
using ProjektWebAPI.Models.DTOs;

namespace ProjektWebAPI.Services;

public interface IDbService
{
   public Task<IEnumerable<TripGetDTO>> GetTripsAsync();
   public Task<IEnumerable<ClientTripDTO>> GetClientTripsAsync(int idClient);
   public Task<int> AddClientAsync(ClientPostDTO dto);
   public Task<bool> RegisterClientToTripAsync(int clientId, int tripId);
   public Task<bool> DeleteClientTripAsync(int clientId, int tripId);
}

public class DbService(IConfiguration configuration) : IDbService
{
    
    public async Task<IEnumerable<TripGetDTO>> GetTripsAsync()//pobieranie wszystkich dostępnych wycieczek oraz ich podstawowych informacji
    {
        var result = new List<TripGetDTO>();
        var connectionString = configuration.GetConnectionString("Default");

        await using var connection = new SqlConnection(connectionString);
        //select wszystkich danych z tabeli trip i połączenie z danymi krajów z tabeli Countries
        var sql = " SELECT t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople, c.Name AS CountryName FROM Trip t JOIN Country_Trip ct ON ct.IdTrip = t.IdTrip JOIN Country c ON c.IdCountry = ct.IdCountry";
        await using var command = new SqlCommand(sql, connection);

        await connection.OpenAsync();

        await using var reader = await command.ExecuteReaderAsync();

        TripGetDTO currentTrip = null;
        int? currenttripId = null;

        while (await reader.ReadAsync())
        {
            var tripId = reader.GetInt32(0);
            

            if (currenttripId != tripId)
            {
                currentTrip = new TripGetDTO
                {
                    IdTrip = tripId,
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    DateFrom = reader.GetDateTime(3),
                    DateTo = reader.GetDateTime(4),
                    MaxPeople = reader.GetInt32(5),
                    Countries = new List<string>()
                };
                result.Add(currentTrip);
                currenttripId = tripId;
            }
            currentTrip.Countries.Add(reader.GetString(6));
        }
        return result;
    }

   public async Task<IEnumerable<ClientTripDTO>> GetClientTripsAsync(int idClient)//pobieranie wszystkich wycieczek dla danego id klienta
{
    var result = new List<ClientTripDTO>();
    var connectionString = configuration.GetConnectionString("Default");

    await using var connection = new SqlConnection(connectionString);
    //wybranie wszystkich danych z tabeli Trips oraz łączenie z danymi dotyczącymi daty rejestracji oraz płatności z tabeli Client
    var sql = "SELECT t.Name, t.Description, t.DateFrom, t.DateTo, ct.RegisteredAt, ct.PaymentDate FROM Client c JOIN Client_Trip ct ON ct.IdClient = c.IdClient JOIN Trip t ON t.IdTrip = ct.IdTrip WHERE c.IdClient = @IdClient";

    await using var command = new SqlCommand(sql, connection);
    command.Parameters.AddWithValue("@IdClient", idClient);

    await connection.OpenAsync();
    await using var reader = await command.ExecuteReaderAsync();

    if (!reader.HasRows)
    {
        reader.Close();

        var checkCmd = new SqlCommand("SELECT 1 FROM Client WHERE IdClient = @IdClient", connection);
        checkCmd.Parameters.AddWithValue("@IdClient", idClient);
        var exists = await checkCmd.ExecuteScalarAsync();

        if (exists == null)
            throw new NotFoundException($"Klient o ID {idClient} nie istnieje.");
        else
            return result;
    }

    while (await reader.ReadAsync())
    {
        int registeredAtInt = reader.GetInt32(4);
        var registeredAt = DateTime.ParseExact(registeredAtInt.ToString(), "yyyyMMdd", null);
        
        DateTime? paymentDate = null;
        if (!reader.IsDBNull(5))
        {
            int paymentDateInt = reader.GetInt32(5);
            paymentDate = DateTime.ParseExact(paymentDateInt.ToString(), "yyyyMMdd", null);
        }

        result.Add(new ClientTripDTO
        {
            Name = reader.GetString(0),
            Description = reader.GetString(1),
            DateFrom = reader.GetDateTime(2),
            DateTo = reader.GetDateTime(3),
            RegisteredAt = registeredAt,
            PaymentDate = paymentDate
        });
    }

    return result;
}

public async Task<int> AddClientAsync(ClientPostDTO dto)//tworzenie nowego klienta
{
    var connectionString = configuration.GetConnectionString("Default");
    await using var connection = new SqlConnection(connectionString);
    //Insertowanie odpowiednich danych
    var sql = "INSERT INTO Client (FirstName, LastName, Email, Telephone, Pesel) OUTPUT INSERTED.IdClient VALUES (@FirstName, @LastName, @Email, @Telephone, @Pesel)";
    await using var command = new SqlCommand(sql, connection);
    command.Parameters.AddWithValue("@FirstName", dto.FirstName);
    command.Parameters.AddWithValue("@LastName", dto.LastName);
    command.Parameters.AddWithValue("@Email", dto.Email);
    command.Parameters.AddWithValue("@Telephone", dto.Telephone);
    command.Parameters.AddWithValue("@Pesel", dto.Pesel);
    
    await connection.OpenAsync();
    var id = (int)await command.ExecuteScalarAsync();
    return id;
}

public async Task<bool> RegisterClientToTripAsync(int clientId, int tripId)//rejestrowanie klienta na konkretną wycieczkę
{
    var connectionString = configuration.GetConnectionString("Default");
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();
    
    var checksql = " SELECT COUNT(1) FROM Client WHERE IdClient = @ClientId; SELECT MaxPeople FROM Trip WHERE IdTrip = @TripId; SELECT COUNT(*) FROM Client_Trip WHERE IdTrip = @TripId;";
    await using var command = new SqlCommand(checksql, connection);
    command.Parameters.AddWithValue("@ClientId", clientId);
    command.Parameters.AddWithValue("@TripId", tripId);
    
    using var reader = await command.ExecuteReaderAsync();
    //sprawdzanie czy znajduje klienta
    if (!reader.Read() || reader.GetInt32(0) == 0)
        throw new NotFoundException("Client not found");
    //sprawdzanie czy znajduje wycieczke
    reader.NextResult();
    if (!reader.Read() || reader.GetInt32(0) == 0)
        throw new NotFoundException("Trip not found");
    int maxPeople = reader.GetInt32(0);
    //sprawdzanie czy jest miejsce na wycieczce
    reader.NextResult();
    int currentCount = reader.Read() ? reader.GetInt32(0) : 0;
    if(currentCount > maxPeople)
        throw new Exception("Maximum people exceeded");
    
    reader.Close();
    
    int registeredAt = int.Parse(DateTime.Now.ToString("yyyyMMdd"));
    //wprowadzanie wycieczki dla klienta(rejestrowanie go)
    var insertsql = "INSERT INTO Client_Trip (IdClient, IdTrip, RegisteredAt) VALUES (@ClientId, @TripId, @RegisteredAt)";
    await using var insertCommand = new SqlCommand(insertsql, connection);
    insertCommand.Parameters.AddWithValue("@ClientId", clientId);
    insertCommand.Parameters.AddWithValue("@TripId", tripId);
    insertCommand.Parameters.AddWithValue("@RegisteredAt", registeredAt);
    await insertCommand.ExecuteNonQueryAsync();
    return true;
}

public async Task<bool> DeleteClientTripAsync(int clientId, int tripId)//usuwanie klienta z wycieczki
{
    var connectionString = configuration.GetConnectionString("Default");
    await using var connection = new SqlConnection(connectionString);
    //usuwanie klienta z wycieczki
    var sql = "DELETE FROM Client_Trip WHERE IdClient = @ClientId AND IdTrip = @TripId";
    await using var command = new SqlCommand(sql, connection);
    command.Parameters.AddWithValue("@ClientId", clientId);
    command.Parameters.AddWithValue("@TripId", tripId);
    
    await connection.OpenAsync();
    var affected = await command.ExecuteNonQueryAsync();
    if (affected == 0)
        throw new NotFoundException("Registration not found");
    return true;
}


}