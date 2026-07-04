using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace GrpcServer.Services
{
    public sealed class OrderService : FleetOperationsService.FleetOperationsServiceBase
    {
        private static readonly Dictionary<string, Order> Orders = [];
        private static readonly object OrdersLock = new();

        public override Task<OrderReply> CreateOrder(CreateOrderRequest request, ServerCallContext context)
        {
            ValidateCreateOrderRequest(request);

            var order = new Order
            {
                OrderId = Guid.NewGuid().ToString(),
                CustomerName = request.CustomerName.Trim(),
                Status = DeliveryStatus.Pending,
                RequestedAt = Timestamp.FromDateTime(DateTime.UtcNow),
                EstimatedDeliveryTime = Duration.FromTimeSpan(TimeSpan.FromHours(24))
            };

            order.Items.AddRange(request.Items.Select(item => item.Clone()));
            order.ExtraInfo.Add(request.ExtraInfo);

            if (request.DeliveryNotes is not null)
            {
                order.DeliveryNotes = request.DeliveryNotes;
            }

            SetPackageDetails(order, request);

            lock (OrdersLock)
            {
                Orders.Add(order.OrderId, order);
            }

            return Task.FromResult(ToOrderReply(order));
        }

        public override Task<OrderReply> GetOrder(GetOrderRequest request, ServerCallContext context)
        {
            var order = GetExistingOrder(request.OrderId);

            return Task.FromResult(ToOrderReply(order));
        }

        public override Task<OrderReply> UpdateOrderStatus(UpdateOrderStatusRequest request, ServerCallContext context)
        {
            if (request.NewStatus == DeliveryStatus.Unspecified)
            {
                throw new RpcException(new Status(
                    StatusCode.InvalidArgument,
                    "NewStatus must be a valid delivery status."));
            }

            Order order;

            lock (OrdersLock)
            {
                if (!Orders.TryGetValue(request.OrderId, out order!))
                {
                    throw CreateOrderNotFoundException(request.OrderId);
                }

                order.Status = request.NewStatus;
            }

            return Task.FromResult(ToOrderReply(order));
        }

        public override Task<ListOrdersReply> ListOrders(ListOrdersRequest request, ServerCallContext context)
        {
            List<Order> orders;

            lock (OrdersLock)
            {
                orders = Orders.Values
                    .Where(order =>
                        request.StatusFilter == DeliveryStatus.Unspecified ||
                        order.Status == request.StatusFilter)
                    .Select(order => order.Clone())
                    .ToList();
            }

            var reply = new ListOrdersReply();
            reply.Orders.AddRange(orders);

            return Task.FromResult(reply);
        }

        private static void ValidateCreateOrderRequest(CreateOrderRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.CustomerName))
            {
                throw new RpcException(new Status(
                    StatusCode.InvalidArgument,
                    "CustomerName is required."));
            }

            if (request.Items.Count == 0)
            {
                throw new RpcException(new Status(
                    StatusCode.InvalidArgument,
                    "At least one order item is required."));
            }

            foreach (var item in request.Items)
            {
                if (string.IsNullOrWhiteSpace(item.ProductName))
                {
                    throw new RpcException(new Status(
                        StatusCode.InvalidArgument,
                        "ProductName is required for every order item."));
                }

                if (item.Quantity <= 0)
                {
                    throw new RpcException(new Status(
                        StatusCode.InvalidArgument,
                        "Quantity must be greater than zero for every order item."));
                }

                if (item.Price < 0)
                {
                    throw new RpcException(new Status(
                        StatusCode.InvalidArgument,
                        "Price cannot be negative."));
                }

                if (item.WeightKg < 0)
                {
                    throw new RpcException(new Status(
                        StatusCode.InvalidArgument,
                        "WeightKg cannot be negative."));
                }
            }
        }

        private static Order GetExistingOrder(string orderId)
        {
            lock (OrdersLock)
            {
                if (Orders.TryGetValue(orderId, out var order))
                {
                    return order;
                }
            }

            throw CreateOrderNotFoundException(orderId);
        }

        private static RpcException CreateOrderNotFoundException(string orderId)
        {
            return new RpcException(new Status(
                StatusCode.NotFound,
                $"Order with id '{orderId}' was not found."));
        }

        private static void SetPackageDetails(Order order, CreateOrderRequest request)
        {
            switch (request.PackageDetailsCase)
            {
                case CreateOrderRequest.PackageDetailsOneofCase.FragilePackage:
                    order.FragilePackage = request.FragilePackage.Clone();
                    break;

                case CreateOrderRequest.PackageDetailsOneofCase.ColdPackage:
                    order.ColdPackage = request.ColdPackage.Clone();
                    break;

                case CreateOrderRequest.PackageDetailsOneofCase.StandardPackage:
                    order.StandardPackage = request.StandardPackage.Clone();
                    break;
            }
        }

        private static OrderReply ToOrderReply(Order order)
        {
            return new OrderReply
            {
                Order = order.Clone()
            };
        }
    }
}
