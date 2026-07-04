using Grpc.Core;
using VehicleGrpcServiceBase = GrpcServer.VehicleService.VehicleServiceBase;
using VehicleMessage = GrpcServer.Vehicle;

namespace GrpcServer.Services
{
    public sealed class VehicleService : VehicleGrpcServiceBase
    {
        private static readonly List<Vehicle> Vehicles = [];
        private static readonly object VehiclesLock = new();

        public override Task<VehicleReply> RegisterVehicle(RegisterVehicleRequest request, ServerCallContext context)
        {
            ValidateRegisterVehicleRequest(request);

            var vehicle = new Vehicle
            {
                VehicleId = Guid.NewGuid().ToString(),
                PlateNumber = request.PlateNumber.Trim(),
                VehicleType = request.VehicleType,
                CapacityKg = request.CapacityKg,
                IsActive = true
            };

            lock (VehiclesLock)
            {
                Vehicles.Add(vehicle);
            }

            return Task.FromResult(ToVehicleReply(vehicle));
        }

        public override Task<VehicleReply> GetVehicle(GetVehicleRequest request, ServerCallContext context)
        {
            Vehicle? vehicle;

            lock (VehiclesLock)
            {
                vehicle = Vehicles.FirstOrDefault(currentVehicle =>
                    currentVehicle.VehicleId == request.VehicleId);
            }

            if (vehicle is null)
            {
                throw new RpcException(new Status(
                    StatusCode.NotFound,
                    $"Vehicle with id '{request.VehicleId}' was not found."));
            }

            return Task.FromResult(ToVehicleReply(vehicle));
        }

        private static void ValidateRegisterVehicleRequest(RegisterVehicleRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PlateNumber))
            {
                throw new RpcException(new Status(
                    StatusCode.InvalidArgument,
                    "PlateNumber is required."));
            }

            if (request.CapacityKg <= 0)
            {
                throw new RpcException(new Status(
                    StatusCode.InvalidArgument,
                    "CapacityKg must be greater than zero."));
            }
        }

        private static VehicleReply ToVehicleReply(Vehicle vehicle)
        {
            return new VehicleReply
            {
                Vehicle = ToVehicleMessage(vehicle)
            };
        }

        private static VehicleMessage ToVehicleMessage(Vehicle vehicle)
        {
            return new VehicleMessage
            {
                VehicleId = vehicle.VehicleId,
                PlateNumber = vehicle.PlateNumber,
                VehicleType = vehicle.VehicleType,
                CapacityKg = vehicle.CapacityKg,
                IsActive = vehicle.IsActive
            };
        }

        private sealed class Vehicle
        {
            public string VehicleId { get; init; } = string.Empty;

            public string PlateNumber { get; init; } = string.Empty;

            public VehicleType VehicleType { get; init; }

            public float CapacityKg { get; init; }

            public bool IsActive { get; init; }
        }
    }
}
