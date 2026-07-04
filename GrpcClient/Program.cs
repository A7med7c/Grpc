using Grpc.Core;
using Grpc.Net.Client;
using GrpcServer;
using FleetOperationsServiceClient = GrpcServer.VehicleService.VehicleServiceClient;

namespace GrpcClient
{
    internal static class Program
    {
        private const string ServerAddress = "https://localhost:7219";

        private static async Task Main()
        {
            using var channel = GrpcChannel.ForAddress(ServerAddress);
            var vehicleClient = new FleetOperationsServiceClient(channel);

            while (true)
            {
                ShowMenu();

                var selectedOption = ReadMenuOption();
                Console.WriteLine();

                switch (selectedOption)
                {
                    case 1:
                        await RegisterVehicleAsync(vehicleClient);
                        break;

                    case 2:
                        await GetVehicleAsync(vehicleClient);
                        break;

                    case 0:
                        return;

                    default:
                        Console.WriteLine("Invalid option. Please choose 1, 2, or 0.");
                        break;
                }

                Console.WriteLine();
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(intercept: true);
                Console.Clear();
            }
        }

        private static void ShowMenu()
        {
            Console.WriteLine("==========================");
            Console.WriteLine("Fleet Vehicle Client");
            Console.WriteLine("==========================");
            Console.WriteLine();
            Console.WriteLine("1. Register Vehicle");
            Console.WriteLine("2. Get Vehicle");
            Console.WriteLine("0. Exit");
            Console.WriteLine();
            Console.Write("Choose an option: ");
        }

        private static int ReadMenuOption()
        {
            var input = Console.ReadLine();

            return int.TryParse(input, out var selectedOption)
                ? selectedOption
                : -1;
        }

        private static async Task RegisterVehicleAsync(FleetOperationsServiceClient vehicleClient)
        {
            try
            {
                Console.Write("Plate Number: ");
                var plateNumber = Console.ReadLine()?.Trim() ?? string.Empty;

                var vehicleType = ReadVehicleType();
                var capacityKg = ReadCapacity();

                var request = new RegisterVehicleRequest
                {
                    PlateNumber = plateNumber,
                    VehicleType = vehicleType,
                    CapacityKg = capacityKg
                };

                var reply = await vehicleClient.RegisterVehicleAsync(request);

                Console.WriteLine();
                Console.WriteLine("Vehicle Registered Successfully");
                Console.WriteLine();
                DisplayVehicle(reply.Vehicle);
            }
            catch (RpcException exception)
            {
                Console.WriteLine(GetFriendlyRpcErrorMessage(exception));
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Unexpected error: {exception.Message}");
            }
        }

        private static async Task GetVehicleAsync(FleetOperationsServiceClient vehicleClient)
        {
            try
            {
                Console.Write("Vehicle Id: ");
                var vehicleId = Console.ReadLine()?.Trim() ?? string.Empty;

                var request = new GetVehicleRequest
                {
                    VehicleId = vehicleId
                };

                var reply = await vehicleClient.GetVehicleAsync(request);

                Console.WriteLine();
                Console.WriteLine("Vehicle Details");
                Console.WriteLine("---------------");
                DisplayVehicle(reply.Vehicle);
            }
            catch (RpcException exception) when (exception.StatusCode == StatusCode.NotFound)
            {
                Console.WriteLine("Vehicle not found.");
            }
            catch (RpcException exception)
            {
                Console.WriteLine(GetFriendlyRpcErrorMessage(exception));
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Unexpected error: {exception.Message}");
            }
        }

        private static VehicleType ReadVehicleType()
        {
            while (true)
            {
                Console.WriteLine("Vehicle Type:");
                Console.WriteLine("1 = VAN");
                Console.WriteLine("2 = TRUCK");
                Console.WriteLine("3 = MOTORBIKE");
                Console.Write("Choose vehicle type: ");

                var input = Console.ReadLine();

                if (int.TryParse(input, out var selectedVehicleType) &&
                    selectedVehicleType is >= 1 and <= 3)
                {
                    return selectedVehicleType switch
                    {
                        1 => VehicleType.Van,
                        2 => VehicleType.Truck,
                        3 => VehicleType.Motorbike,
                        _ => VehicleType.Unknown
                    };
                }

                Console.WriteLine("Invalid vehicle type. Please choose 1, 2, or 3.");
                Console.WriteLine();
            }
        }

        private static float ReadCapacity()
        {
            while (true)
            {
                Console.Write("Capacity (Kg): ");
                var input = Console.ReadLine();

                if (float.TryParse(input, out var capacityKg) && capacityKg > 0)
                {
                    return capacityKg;
                }

                Console.WriteLine("Capacity must be a number greater than zero.");
            }
        }

        private static void DisplayVehicle(Vehicle vehicle)
        {
            Console.WriteLine($"Vehicle Id: {vehicle.VehicleId}");
            Console.WriteLine($"Plate Number: {vehicle.PlateNumber}");
            Console.WriteLine($"Vehicle Type: {vehicle.VehicleType}");
            Console.WriteLine($"Capacity (Kg): {vehicle.CapacityKg}");
            Console.WriteLine($"Is Active: {vehicle.IsActive}");
        }

        private static string GetFriendlyRpcErrorMessage(RpcException exception)
        {
            return exception.StatusCode switch
            {
                StatusCode.InvalidArgument => $"Invalid request: {exception.Status.Detail}",
                StatusCode.Unavailable => "Unable to connect to the Vehicle gRPC server. Make sure the server is running.",
                _ => $"gRPC error: {exception.Status.Detail}"
            };
        }
    }
}

