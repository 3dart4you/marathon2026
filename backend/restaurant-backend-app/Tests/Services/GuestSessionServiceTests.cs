using Amazon.CognitoIdentityProvider.Model;
using APIHandler.Constants.Dishes;
using APIHandler.Constants.Orders;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.GuestSessions;
using APIHandler.Repositories.Auth;
using APIHandler.Repositories.Dishes;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.GuestSessions;
using AutoFixture.Xunit2;
using Moq;
using NPOI.POIFS.Storage;
using System.Security.Cryptography;
using System.Text;

namespace APIHandler.Tests.Services;

public class GuestSessionServiceTests
{
    private readonly Mock<IGuestSessionRepository> _mockGuestSessionRepository = new();
    private readonly Mock<IGroupReservationRepository> _mockGroupReservationRepository = new();
    private readonly Mock<IDishRepository> _mockDishRepository = new();
    private readonly Mock<IReservationRepository> _mockReservationRepository = new();
    private readonly Mock<IUserRepository> _mockUserRepository = new();
    private readonly Mock<ITableRepository> _mockTableRepository = new();
    private readonly GuestSessionService _guestSessionService;

    public GuestSessionServiceTests()
    {
        _guestSessionService = new GuestSessionService(
            _mockGroupReservationRepository.Object,
            _mockGuestSessionRepository.Object,
            _mockDishRepository.Object,
            _mockReservationRepository.Object,
            _mockUserRepository.Object,
            _mockTableRepository.Object);
    }
    [Theory]
    [InlineAutoData(0)]
    [InlineAutoData(3)]
    public async Task CreateGuestSessionAsync_CorrectData_ReturnsSessionToken(
        int guestCount,
        CreateGuestSessionRequest request,
        GroupReservation groupReservation,
        Reservation parentReservation)
    {
        groupReservation.GroupReservationId = request.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;
        groupReservation.ReservationId = parentReservation.Id;

        parentReservation.GuestCount = 5;

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(groupReservation);

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(parentReservation.Id))
            .ReturnsAsync(parentReservation);

        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync([]);

        GuestSession? session = null;
        _mockGuestSessionRepository
            .Setup(r => r.SaveAsync(It.IsAny<GuestSession>()))
            .Callback<GuestSession>(s => session = s);

        _mockGuestSessionRepository
            .Setup(r => r.CountByGroupReservationId(groupReservation.GroupReservationId))
            .ReturnsAsync(guestCount);

        var response = await _guestSessionService.CreateGuestSessionAsync(request);

        Assert.NotEmpty(response.SessionId);
        Assert.NotEmpty(response.SessionToken);
        Assert.NotNull(session);
        Assert.Equal(groupReservation.GroupReservationId, session.GroupReservationId);
        Assert.Equal(request.GuestName, session.GuestName);
        Assert.NotNull(session.SessionTokenHash);
        Assert.NotEmpty(session.SessionTokenHash);
    }

    [Theory]
    [AutoData]
    public async Task CreateGuestSessionAsync_InvalidGroupReservationId_ThrowsGroupReservationNotFoundException(CreateGuestSessionRequest request)
    {
        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(request.GroupReservationId))
            .ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() => _guestSessionService.CreateGuestSessionAsync(request));
    }

    [Theory]
    [AutoData]
    public async Task CreateGuestSessionAsync_GroupReservationExpired_ThrowsGroupReservationExpiredException(CreateGuestSessionRequest request, GroupReservation groupReservation)
    {
        groupReservation.GroupReservationId = request.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(request.GroupReservationId))
            .ReturnsAsync(groupReservation);

        await Assert.ThrowsAsync<GroupReservationExpiredException>(() => _guestSessionService.CreateGuestSessionAsync(request));
    }

    [Theory]
    [AutoData]
    public async Task AddDishAsync_ShouldAddNewDish_WhenDishDoesNotExistInSession(GuestSession guestSession, GroupReservation groupReservation, Reservation parentReservation, Table table, Dish dish)
    {
        guestSession.Dishes = [];
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;

        groupReservation.ReservationId = parentReservation.Id;
        parentReservation.TableId = table.Id;
        dish.State = DishState.Available;
        int quantityToAdd = 2;

        _mockGuestSessionRepository.Setup(r => r.GetByGuestSessionId(guestSession.Id)).ReturnsAsync(guestSession);
        _mockGroupReservationRepository.Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId)).ReturnsAsync(groupReservation);
        _mockReservationRepository.Setup(r => r.GetReservationByIdAsync(parentReservation.Id)).ReturnsAsync(parentReservation);
        _mockTableRepository.Setup(r => r.GetTableAsync(table.Id)).ReturnsAsync(table);
        _mockDishRepository.Setup(r => r.GetDishesByLocationIdAsync(table.LocationId)).ReturnsAsync([dish]);

        await _guestSessionService.AddDishAsync(guestSession.Id, dish.Id, quantityToAdd);

        Assert.Single(guestSession.Dishes);
        Assert.Equal(dish.Id, guestSession.Dishes[0].DishId);
        Assert.Equal(quantityToAdd, guestSession.Dishes[0].Quantity);
        Assert.Equal(OrderItemState.Pending, guestSession.Dishes[0].State);

        _mockGuestSessionRepository.Verify(r => r.SaveAsync(guestSession), Times.Once);
    }

    [Theory]
    [AutoData]
    public async Task AddDishAsync_ShouldIncreaseQuantity_WhenDishAlreadyExists(GuestSession guestSession, GroupReservation groupReservation, Reservation parentReservation, Table table, Dish dish)
    {
        int initialQuantity = 3;
        int quantityToAdd = 2;
        guestSession.Dishes = [new OrderItem { DishId = dish.Id, Quantity = initialQuantity, State = OrderItemState.Pending }];
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;

        groupReservation.ReservationId = parentReservation.Id;
        parentReservation.TableId = table.Id;
        dish.State = DishState.Available;

        _mockGuestSessionRepository.Setup(r => r.GetByGuestSessionId(guestSession.Id)).ReturnsAsync(guestSession);
        _mockGroupReservationRepository.Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId)).ReturnsAsync(groupReservation);
        _mockReservationRepository.Setup(r => r.GetReservationByIdAsync(parentReservation.Id)).ReturnsAsync(parentReservation);
        _mockTableRepository.Setup(r => r.GetTableAsync(table.Id)).ReturnsAsync(table);
        _mockDishRepository.Setup(r => r.GetDishesByLocationIdAsync(table.LocationId)).ReturnsAsync([dish]);

        await _guestSessionService.AddDishAsync(guestSession.Id, dish.Id, quantityToAdd);

        Assert.Single(guestSession.Dishes);
        Assert.Equal(initialQuantity + quantityToAdd, guestSession.Dishes[0].Quantity);

        _mockGuestSessionRepository.Verify(r => r.SaveAsync(guestSession), Times.Once);
    }

    [Theory]
    [InlineAutoData(0)]
    [InlineAutoData(-1)]
    public async Task AddDishAsync_ShouldThrowValidationException_WhenQuantityIsZeroOrLess(int quantity, string guestSessionId, Dish dish)
    {
        dish.State = DishState.Available;

        _mockDishRepository
            .Setup(r => r.GetDishByIdAsync(dish.Id))
            .ReturnsAsync(dish);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _guestSessionService.AddDishAsync(guestSessionId, dish.Id, quantity));
    }

    [Theory]
    [AutoData]
    public async Task AddDishAsync_ShouldThrowGuestSessionNotFoundException_WhenSessionNotFound(string guestSessionId, Dish dish)
    {
        dish.State = DishState.Available;

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSessionId))
            .ReturnsAsync((GuestSession?)null);

        _mockDishRepository
            .Setup(r => r.GetDishByIdAsync(dish.Id))
            .ReturnsAsync(dish);

        var exception = await Assert.ThrowsAsync<GuestSessionNotFoundException>(() =>
            _guestSessionService.AddDishAsync(guestSessionId, dish.Id, 2));
    }

    [Theory]
    [AutoData]
    public async Task AddDishAsync_ShouldThrowDishNotFound_WhenDishNotFound(GuestSession guestSession, GroupReservation groupReservation, Reservation parentReservation, Table table, string dishId)
    {
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;
        groupReservation.ReservationId = parentReservation.Id;
        parentReservation.TableId = table.Id;

        _mockGuestSessionRepository.Setup(r => r.GetByGuestSessionId(guestSession.Id)).ReturnsAsync(guestSession);
        _mockGroupReservationRepository.Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId)).ReturnsAsync(groupReservation);
        _mockReservationRepository.Setup(r => r.GetReservationByIdAsync(parentReservation.Id)).ReturnsAsync(parentReservation);
        _mockTableRepository.Setup(r => r.GetTableAsync(table.Id)).ReturnsAsync(table);

        _mockDishRepository.Setup(r => r.GetDishesByLocationIdAsync(table.LocationId)).ReturnsAsync([]);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            _guestSessionService.AddDishAsync(guestSession.Id, dishId, quantity: 2));
    }

    [Theory]
    [AutoData]
    public async Task AddDishAsync_ShouldThrowDishNotAvailable_WhenDishIsNotAvailable(GuestSession guestSession, GroupReservation groupReservation, Reservation parentReservation, Table table, Dish dish)
    {
        dish.State = DishState.OutOfStock;
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;
        groupReservation.ReservationId = parentReservation.Id;
        parentReservation.TableId = table.Id;

        _mockGuestSessionRepository.Setup(r => r.GetByGuestSessionId(guestSession.Id)).ReturnsAsync(guestSession);
        _mockGroupReservationRepository.Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId)).ReturnsAsync(groupReservation);
        _mockReservationRepository.Setup(r => r.GetReservationByIdAsync(parentReservation.Id)).ReturnsAsync(parentReservation);
        _mockTableRepository.Setup(r => r.GetTableAsync(table.Id)).ReturnsAsync(table);
        _mockDishRepository.Setup(r => r.GetDishesByLocationIdAsync(table.LocationId)).ReturnsAsync([dish]);

        var exception = await Assert.ThrowsAsync<DishIsNotAvailableException>(() =>
            _guestSessionService.AddDishAsync(guestSession.Id, dish.Id, quantity: 2));
    }

    [Theory]
    [InlineAutoData(GroupReservationStatus.Submitted, typeof(GroupReservationSubmittedException))]
    [InlineAutoData(GroupReservationStatus.Closed, typeof(GroupReservationExpiredException))]
    public async Task AddDishAsync_ShouldThrowGroupReservationExpired_WhenGroupStatusIsNotOpen(GroupReservationStatus status, Type exceptionType, GuestSession guestSession, GroupReservation groupReservation, Dish dish)
    {
        dish.State = DishState.Available;
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.Status = status;

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSession.Id))
            .ReturnsAsync(guestSession);

        _mockDishRepository
            .Setup(r => r.GetDishByIdAsync(dish.Id))
            .ReturnsAsync(dish);

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(groupReservation);

        var exception = await Assert.ThrowsAsync(exceptionType, () =>
            _guestSessionService.AddDishAsync(guestSession.Id, dish.Id, quantity: 2));
    }

    [Theory]
    [AutoData]
    public async Task RemoveDishAsync_ShouldDecreaseQuantity_WhenQuantityIsGreaterThanAmountToRemove(GuestSession guestSession, GroupReservation groupReservation, string dishId)
    {
        int initialQuantity = 5;
        int quantityToRemove = 2;
        guestSession.Dishes =
        [
            new OrderItem { DishId = dishId, Quantity = initialQuantity, State = OrderItemState.Pending }
        ];
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSession.Id))
            .ReturnsAsync(guestSession);


        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(groupReservation);

        await _guestSessionService.RemoveDishAsync(guestSession.Id, dishId, quantityToRemove);

        Assert.Single(guestSession.Dishes);
        Assert.Equal(initialQuantity - quantityToRemove, guestSession.Dishes[0].Quantity);

        _mockGuestSessionRepository.Verify(r => r.SaveAsync(guestSession), Times.Once);
    }

    [Theory]
    [AutoData]
    public async Task RemoveDishAsync_ShouldRemoveDishFromList_WhenQuantityToRemoveIsEqualOrGreater(GuestSession guestSession, GroupReservation groupReservation, string dishId)
    {
        int initialQuantity = 2;
        int quantityToRemove = 3;
        guestSession.Dishes =
        [
            new OrderItem { DishId = dishId, Quantity = initialQuantity, State = OrderItemState.Pending }
        ];
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSession.Id))
            .ReturnsAsync(guestSession);

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(groupReservation);

        await _guestSessionService.RemoveDishAsync(guestSession.Id, dishId, quantityToRemove);

        Assert.Empty(guestSession.Dishes);
        _mockGuestSessionRepository.Verify(r => r.SaveAsync(guestSession), Times.Once);
    }

    [Theory]
    [AutoData]
    public async Task RemoveDishAsync_ShouldThrowDishNotFoundException_WhenDishDoesNotExistInSession(GuestSession guestSession, GroupReservation groupReservation, string dishId)
    {
        guestSession.Dishes = [];
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSession.Id))
            .ReturnsAsync(guestSession);

        _mockGroupReservationRepository
           .Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId))
           .ReturnsAsync(groupReservation);

        var exception = await Assert.ThrowsAsync<DishNotFoundException>(() =>
            _guestSessionService.RemoveDishAsync(guestSession.Id, dishId, 2));

        Assert.Contains("not found in guest session", exception.Message);
    }

    [Theory]
    [AutoData]
    public async Task RemoveDishAsync_ShouldThrowValidationException_WhenQuantityIsZeroOrLess(string guestSessionId, string dishId)
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _guestSessionService.RemoveDishAsync(guestSessionId, dishId, 0));

        await Assert.ThrowsAsync<ValidationException>(() =>
            _guestSessionService.RemoveDishAsync(guestSessionId, dishId, -5));
    }

    [Theory]
    [AutoData]
    public async Task RemoveDishAsync_ShouldThrowGuestSessionNotFoundException_WhenSessionNotFound(string guestSessionId, string dishId)
    {
        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSessionId))
            .ReturnsAsync((GuestSession?)null);

        var exception = await Assert.ThrowsAsync<GuestSessionNotFoundException>(() =>
            _guestSessionService.RemoveDishAsync(guestSessionId, dishId, 2));
    }
    [Theory]
    [InlineAutoData(1, 0)]
    [InlineAutoData(1, 1)]
    [InlineAutoData(2, 1)]
    [InlineAutoData(2, 2)]
    public async Task CreateGuestSessionAsync_WhenCapacityReached_ThrowsGuestLimitExceededException(
        int reservationCapacity,
        int guestCount,
        CreateGuestSessionRequest request,
        GroupReservation groupReservation,
        Reservation parentReservation)
    {
        groupReservation.Status = GroupReservationStatus.Open;
        groupReservation.GroupReservationId = request.GroupReservationId;
        groupReservation.ReservationId = parentReservation.Id;

        parentReservation.GuestCount = reservationCapacity;

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(groupReservation);

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(parentReservation.Id))
            .ReturnsAsync(parentReservation);

        _mockGuestSessionRepository
            .Setup(r => r.CountByGroupReservationId(groupReservation.GroupReservationId))
            .ReturnsAsync(guestCount);

        var exception = await Assert.ThrowsAsync<GuestLimitExceededException>(() => _guestSessionService.CreateGuestSessionAsync(request));

        _mockGuestSessionRepository.Verify(r => r.SaveAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Theory]
    [AutoData]
    public async Task CreateGuestSessionAsync_MissingParentReservation_ThrowsGroupReservationExpiredException(
        CreateGuestSessionRequest request,
        GroupReservation groupReservation)
    {
        groupReservation.GroupReservationId = request.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(groupReservation);

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(groupReservation.ReservationId))
            .ReturnsAsync((Reservation?)null);

        var exception = await Assert.ThrowsAsync<GroupReservationExpiredException>(() => _guestSessionService.CreateGuestSessionAsync(request));
    }

    [Theory]
    [AutoData]
    public async Task IsAuthorizedAsync_ValidTokenAndId_ReturnsTrue(GuestSession guestSession, byte[] tokenBytes)
    {
        string tokenString = Convert.ToBase64String(tokenBytes);
        var hashString = Convert.ToBase64String(SHA256.HashData(tokenBytes));
        guestSession.SessionTokenHash = hashString;

        _mockGuestSessionRepository
            .Setup(r => r.GetByTokenHash(hashString))
            .ReturnsAsync(guestSession);

        bool result = await _guestSessionService.IsAuthorizedAsync(tokenString, guestSession.Id);

        Assert.True(result);
    }

    [Theory]
    [AutoData]
    public async Task IsAuthorizedAsync_InvalidToken_ReturnsFalse(byte[] tokenBytes, string guestSessionId)
    {
        string tokenString = Convert.ToBase64String(tokenBytes);
        var hashString = Convert.ToBase64String(SHA256.HashData(tokenBytes));

        _mockGuestSessionRepository
            .Setup(r => r.GetByTokenHash(hashString))
            .ReturnsAsync((GuestSession?)null);

        bool result = await _guestSessionService.IsAuthorizedAsync(tokenString, guestSessionId);

        Assert.False(result);
    }

    [Theory]
    [AutoData]
    public async Task IsAuthorizedAsync_InvalidGuestSessionId_ReturnsFalse(GuestSession guestSession, byte[] tokenBytes, string guestSessionId)
    {
        string tokenString = Convert.ToBase64String(tokenBytes);
        var hashString = Convert.ToBase64String(SHA256.HashData(tokenBytes));
        guestSession.SessionTokenHash = hashString;

        _mockGuestSessionRepository
            .Setup(r => r.GetByTokenHash(hashString))
            .ReturnsAsync(guestSession);

        bool result = await _guestSessionService.IsAuthorizedAsync(tokenString, guestSessionId);

        Assert.False(result);
    }
    [Theory]
    [AutoData]
    public async Task SubmitCartAsync_Valid_SubmitsSuccessfuilly(GuestSession guestSession, GroupReservation groupReservation, Dish[] dishes)
    {
        foreach (var dish in dishes)
            dish.State = DishState.Available;

        guestSession.IsSubmitted = false;
        guestSession.GroupReservationId = groupReservation.GroupReservationId;

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSession.Id))
            .ReturnsAsync(guestSession);

        _mockGroupReservationRepository
           .Setup(r => r.GetGroupReservationByIdAsync(guestSession.GroupReservationId))
           .ReturnsAsync(groupReservation);

        _mockDishRepository
            .Setup(d => d.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(dishes);

        GuestSession? session = null;
        _mockGuestSessionRepository
            .Setup(r => r.SaveAsync(It.IsAny<GuestSession>()))
            .Callback<GuestSession>(s => session = s);

        await _guestSessionService.SubmitCartAsync(guestSession.Id);

        Assert.NotNull(session);
        Assert.True(session.IsSubmitted);
    }

    [Theory]
    [AutoData]
    public async Task SubmitCartAsync_SessionNotFound_ThrowsGuestSessionNotFound(string guestSessionId)
    {
        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSessionId))
            .ReturnsAsync((GuestSession?)null);

        await Assert.ThrowsAsync<GuestSessionNotFoundException>(() => _guestSessionService.SubmitCartAsync(guestSessionId));
    }

    [Theory]
    [InlineAutoData(GroupReservationStatus.Submitted, typeof(GroupReservationSubmittedException))]
    [InlineAutoData(GroupReservationStatus.Closed, typeof(GroupReservationExpiredException))]
    public async Task SubmitCartAsync_GroupReservationNotOpen_ThrowsException(GroupReservationStatus status, Type exceptionType, GuestSession guestSession, GroupReservation groupReservation)
    {
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.Status = status;

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSession.Id))
            .ReturnsAsync(guestSession);

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(guestSession.GroupReservationId))
            .ReturnsAsync(groupReservation);

        await Assert.ThrowsAsync(exceptionType, () => _guestSessionService.SubmitCartAsync(guestSession.Id));
    }

    [Theory]
    [AutoData]
    public async Task SubmitCartAsync_GuestSessionHasNoDishes_ThrowsCartIsEmptyException(GuestSession guestSession, GroupReservation groupReservation)
    {
        guestSession.Dishes = [];
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSession.Id))
            .ReturnsAsync(guestSession);

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(guestSession.GroupReservationId))
            .ReturnsAsync(groupReservation);

        await Assert.ThrowsAsync<CartIsEmptyException>(() => _guestSessionService.SubmitCartAsync(guestSession.Id));
    }

    [Theory]
    [AutoData]
    public async Task SubmitCartAsync_GuestSessionSubmitted_ThrowsAlreadySubmited(GuestSession guestSession, GroupReservation groupReservation)
    {
        guestSession.IsSubmitted = true;
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSession.Id))
            .ReturnsAsync(guestSession);

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(guestSession.GroupReservationId))
            .ReturnsAsync(groupReservation);

        await Assert.ThrowsAsync<GuestSessionSubmittedException>(() => _guestSessionService.SubmitCartAsync(guestSession.Id));
    }

    [Theory]
    [InlineAutoData(DishState.OutOfStock)]
    [InlineAutoData(DishState.OnStop)]
    public async Task SubmitCartAsync_DishNotAvailable_ThrowsDishNotAvailable(DishState state, Dish dish, GuestSession guestSession, GroupReservation groupReservation)
    {
        dish.State = state;
        guestSession.IsSubmitted = false;
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        guestSession.Dishes = 
        [
            new OrderItem()
            {
                DishId = dish.Id,
                Quantity = 1,
            }
        ];

        _mockDishRepository
            .Setup(d => d.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync([dish]);

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSession.Id))
            .ReturnsAsync(guestSession);

        _mockGroupReservationRepository
           .Setup(r => r.GetGroupReservationByIdAsync(guestSession.GroupReservationId))
           .ReturnsAsync(groupReservation);

        await Assert.ThrowsAsync<DishIsNotAvailableException>(() => _guestSessionService.SubmitCartAsync(guestSession.Id));
    }
    [Theory]
    [MemberAutoData(nameof(PepareDishesForGuestSession))]
    public async Task GetGuestSessionInfoAsync_Guest_ReturnsAsync(
        IEnumerable<string> dishIds,
        IEnumerable<Dish> dishes,
        List<OrderItem> orderItems,
        decimal expectedTotal,
        GuestSession session)
    {
        session.Dishes = orderItems;
        session.IsOwnerSession = false;

        _mockDishRepository
            .Setup(r => r.GetDishesByIdsAsync(dishIds))
            .ReturnsAsync(dishes);

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(session.Id))
            .ReturnsAsync(session);

        GetGuestSessionResponse result = await _guestSessionService.GetGuestSessionInfoAsync(session.Id);

        Assert.Equal(session.GuestName, result.Name);
        Assert.Equal(dishes.Count(), result.Items.Count());
        Assert.Equal(expectedTotal, result.Total);
    }

    [Theory]
    [MemberAutoData(nameof(PepareDishesForGuestSession))]
    public async Task GetGuestSessionInfoAsync_GroupOwner_ReturnsAsync(
        IEnumerable<string> dishIds,
        IEnumerable<Dish> dishes,
        List<OrderItem> orderItems,
        decimal expectedTotal,
        GuestSession session,
        GroupReservation groupReservation,
        Reservation reservation,
        string customerId,
        string ownerName,
        UserType userType)
    {
        session.Dishes = orderItems;
        session.IsOwnerSession = true;
        session.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.ReservationId = reservation.Id;
        reservation.CustomerId = customerId;
        userType.Attributes.Add(new AttributeType()
        {
            Name = "given_name",
            Value = ownerName,
        });

        _mockDishRepository
            .Setup(r => r.GetDishesByIdsAsync(dishIds))
            .ReturnsAsync(dishes);

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(session.Id))
            .ReturnsAsync(session);

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(groupReservation);

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _mockUserRepository
            .Setup(r => r.GetUserByIdAsync(customerId))
            .ReturnsAsync(userType);

        GetGuestSessionResponse result = await _guestSessionService.GetGuestSessionInfoAsync(session.Id);

        Assert.Equal(ownerName, result.Name);
        Assert.Equal(dishes.Count(), result.Items.Count());
        Assert.Equal(expectedTotal, result.Total);
    }

    public static TheoryData<IEnumerable<string>, IEnumerable<Dish>, List<OrderItem>, decimal> PepareDishesForGuestSession()
    {
        var dishMap = new Dictionary<string, Dish>()
        {
            ["1"] = new()
            {
                Id = "1",
                Price = 1
            },
            ["2"] = new()
            {
                Id = "2",
                Price = 2
            },
            ["3"] = new()
            {
                Id = "3",
                Price = 3
            }
        };

        var orderItems = new List<OrderItem>
        {
            new OrderItem()
                    {
                        DishId = "1",
                        Quantity = 2
                    },
                    new OrderItem()
                    {
                        DishId = "2",
                        Quantity = 3
                    },
                    new OrderItem()
                    {
                        DishId = "3",
                        Quantity = 1
                    }
        };

        var expectedTotal = orderItems.Sum(o => dishMap[o.DishId].Price * o.Quantity);

        return new()
        {
            { dishMap.Keys, dishMap.Values, orderItems, expectedTotal }
        };
    }

    [Theory]
    [AutoData]
    public async Task GetGroupSessionsAsync_SeveralGuestsWithItems_ReturnsParticipantsWithTotals(
        string userId, GroupReservation group, Reservation reservation)
    {
        const decimal saladPrice = 180m;
        const decimal soupPrice = 110m;
        SetupOwnedGroup(group, reservation, userId);
        var salad = CreateDish("dish-salad", saladPrice);
        var soup = CreateDish("dish-soup", soupPrice);
        var first = CreateSession("session-1", group.GroupReservationId, items: [CreateItem(salad.Id, 2)]);
        var second = CreateSession("session-2", group.GroupReservationId, items: [CreateItem(soup.Id, 2), CreateItem(salad.Id, 1)]);
        SetupSessions(group.GroupReservationId, first, second);
        SetupDishes(salad, soup);

        var result = await _guestSessionService.GetGroupSessionsAsync(userId, group.GroupReservationId);

        Assert.Collection(result.Participants,
            p =>
            {
                Assert.Equal(first.Id, p.Id);
                Assert.Equal(first.GuestName, p.Name);
                Assert.Equal(2 * saladPrice, p.Total);
                var item = Assert.Single(p.Items);
                Assert.Equal(salad.Id, item.Dish.Id);
                Assert.Equal(saladPrice, item.Dish.Price);
                Assert.Equal(2, item.Quantity);
            },
            p =>
            {
                Assert.Equal(second.Id, p.Id);
                Assert.Equal(2 * soupPrice + saladPrice, p.Total);
                Assert.Collection(p.Items,
                    i =>
                    {
                        Assert.Equal(soup.Id, i.Dish.Id);
                        Assert.Equal(soupPrice, i.Dish.Price);
                        Assert.Equal(2, i.Quantity);
                    },
                    i =>
                    {
                        Assert.Equal(salad.Id, i.Dish.Id);
                        Assert.Equal(1, i.Quantity);
                    });
            });
        Assert.Equal(3 * saladPrice + 2 * soupPrice, result.Total);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupSessionsAsync_OwnerSessionInMiddle_ReturnsOwnerFirstThenGuestsInRepositoryOrder(
        string userId, GroupReservation group, Reservation reservation)
    {
        SetupOwnedGroup(group, reservation, userId);
        var guest1 = CreateSession("guest-1", group.GroupReservationId);
        var owner = CreateSession("owner", group.GroupReservationId, isOwner: true);
        var guest2 = CreateSession("guest-2", group.GroupReservationId);
        SetupSessions(group.GroupReservationId, guest1, owner, guest2);

        var result = await _guestSessionService.GetGroupSessionsAsync(userId, group.GroupReservationId);

        Assert.Collection(result.Participants,
            p =>
            {
                Assert.Equal(owner.Id, p.Id);
                Assert.Equal(ParticipantType.Owner, p.Type);
            },
            p =>
            {
                Assert.Equal(guest1.Id, p.Id);
                Assert.Equal(ParticipantType.Guest, p.Type);
            },
            p =>
            {
                Assert.Equal(guest2.Id, p.Id);
                Assert.Equal(ParticipantType.Guest, p.Type);
            });
    }

    [Theory]
    [AutoData]
    public async Task GetGroupSessionsAsync_NoSessions_ReturnsEmptyParticipantsAndZeroTotal(
        string userId, GroupReservation group, Reservation reservation)
    {
        SetupOwnedGroup(group, reservation, userId);
        SetupSessions(group.GroupReservationId);

        var result = await _guestSessionService.GetGroupSessionsAsync(userId, group.GroupReservationId);

        Assert.Empty(result.Participants);
        Assert.Equal(0m, result.Total);
        _mockDishRepository.Verify(r => r.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupSessionsAsync_SessionsWithoutDishes_ReturnsZeroTotalsWithoutLoadingDishes(
        string userId, GroupReservation group, Reservation reservation)
    {
        SetupOwnedGroup(group, reservation, userId);
        var first = CreateSession("session-1", group.GroupReservationId);
        var second = CreateSession("session-2", group.GroupReservationId);
        SetupSessions(group.GroupReservationId, first, second);

        var result = await _guestSessionService.GetGroupSessionsAsync(userId, group.GroupReservationId);

        Assert.Equal(2, result.Participants.Count());
        Assert.All(result.Participants, p =>
        {
            Assert.Empty(p.Items);
            Assert.Equal(0m, p.Total);
        });
        Assert.Equal(0m, result.Total);
        _mockDishRepository.Verify(r => r.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupSessionsAsync_SessionDishesNull_ReturnsEmptyItems(
        string userId, GroupReservation group, Reservation reservation)
    {
        const decimal price = 50m;
        SetupOwnedGroup(group, reservation, userId);
        var dish = CreateDish("dish-1", price);
        var withoutDishes = CreateSession("session-null", group.GroupReservationId);
        withoutDishes.Dishes = null!;
        var withDishes = CreateSession("session-items", group.GroupReservationId, items: [CreateItem(dish.Id, 1)]);
        SetupSessions(group.GroupReservationId, withoutDishes, withDishes);
        SetupDishes(dish);

        var result = await _guestSessionService.GetGroupSessionsAsync(userId, group.GroupReservationId);

        var nullParticipant = Assert.Single(result.Participants, p => p.Id == withoutDishes.Id);
        Assert.Empty(nullParticipant.Items);
        Assert.Equal(0m, nullParticipant.Total);
        Assert.Equal(price, result.Total);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupSessionsAsync_DishMissingFromRepository_SkipsItemAndExcludesFromTotals(
        string userId, GroupReservation group, Reservation reservation)
    {
        const decimal price = 75m;
        SetupOwnedGroup(group, reservation, userId);
        var existing = CreateDish("dish-existing", price);
        var session = CreateSession("session-1", group.GroupReservationId,
            items: [CreateItem(existing.Id, 2), CreateItem("dish-deleted", 3)]);
        SetupSessions(group.GroupReservationId, session);
        SetupDishes(existing);

        var result = await _guestSessionService.GetGroupSessionsAsync(userId, group.GroupReservationId);

        var participant = Assert.Single(result.Participants);
        var item = Assert.Single(participant.Items);
        Assert.Equal(existing.Id, item.Dish.Id);
        Assert.Equal(2 * price, participant.Total);
        Assert.Equal(2 * price, result.Total);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupSessionsAsync_SameDishInSeveralSessions_RequestsDistinctDishIdsAndResolvesEverySession(
        string userId, GroupReservation group, Reservation reservation)
    {
        const decimal sharedPrice = 20m;
        const decimal otherPrice = 30m;
        SetupOwnedGroup(group, reservation, userId);
        var shared = CreateDish("dish-shared", sharedPrice);
        var other = CreateDish("dish-other", otherPrice);
        var first = CreateSession("session-1", group.GroupReservationId, items: [CreateItem(shared.Id, 1)]);
        var second = CreateSession("session-2", group.GroupReservationId, items: [CreateItem(shared.Id, 2), CreateItem(other.Id, 1)]);
        var third = CreateSession("session-3", group.GroupReservationId, items: [CreateItem(shared.Id, 3)]);
        SetupSessions(group.GroupReservationId, first, second, third);
        List<string>? requestedIds = null;
        _mockDishRepository
            .Setup(r => r.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()))
            .Callback<IEnumerable<string>>(ids => requestedIds = ids.ToList())
            .ReturnsAsync([shared, other]);

        var result = await _guestSessionService.GetGroupSessionsAsync(userId, group.GroupReservationId);

        Assert.NotNull(requestedIds);
        Assert.Equal(2, requestedIds.Count);
        Assert.Contains(shared.Id, requestedIds);
        Assert.Contains(other.Id, requestedIds);
        _mockDishRepository.Verify(r => r.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()), Times.Once);
        Assert.All(result.Participants, p => Assert.Contains(p.Items, i => i.Dish.Id == shared.Id));
        Assert.Equal(6 * sharedPrice + otherPrice, result.Total);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupSessionsAsync_GroupNotFound_ThrowsGroupReservationNotFoundException(
        string userId, string groupReservationId)
    {
        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(
            () => _guestSessionService.GetGroupSessionsAsync(userId, groupReservationId));

        _mockGuestSessionRepository.Verify(r => r.GetAllByGroupReservationIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupSessionsAsync_ParentReservationNotFound_ThrowsReservationNotFoundException(
        string userId, GroupReservation group)
    {
        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(group.GroupReservationId))
            .ReturnsAsync(group);
        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(group.ReservationId))
            .ReturnsAsync((Reservation?)null);

        await Assert.ThrowsAsync<ReservationNotFoundException>(
            () => _guestSessionService.GetGroupSessionsAsync(userId, group.GroupReservationId));

        _mockGuestSessionRepository.Verify(r => r.GetAllByGroupReservationIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupSessionsAsync_UserIsNotReservationOwner_ThrowsActionForbiddenException(
        string userId, string otherCustomerId, GroupReservation group, Reservation reservation)
    {
        SetupOwnedGroup(group, reservation, otherCustomerId);

        await Assert.ThrowsAsync<ActionForbiddenException>(
            () => _guestSessionService.GetGroupSessionsAsync(userId, group.GroupReservationId));

        _mockGuestSessionRepository.Verify(r => r.GetAllByGroupReservationIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupSessionsAsync_ReservationWithoutCustomer_ThrowsActionForbiddenException(
        string userId, GroupReservation group, Reservation reservation)
    {
        SetupOwnedGroup(group, reservation, userId);
        reservation.CustomerId = null;

        await Assert.ThrowsAsync<ActionForbiddenException>(
            () => _guestSessionService.GetGroupSessionsAsync(userId, group.GroupReservationId));

        _mockGuestSessionRepository.Verify(r => r.GetAllByGroupReservationIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineAutoData(GroupReservationStatus.Open)]
    [InlineAutoData(GroupReservationStatus.Submitted)]
    [InlineAutoData(GroupReservationStatus.Closed)]
    public async Task GetGroupSessionsAsync_AnyGroupStatus_ReturnsSessions(
        GroupReservationStatus status, string userId, GroupReservation group, Reservation reservation)
    {
        SetupOwnedGroup(group, reservation, userId);
        group.Status = status;
        var session = CreateSession("session-1", group.GroupReservationId);
        SetupSessions(group.GroupReservationId, session);

        var result = await _guestSessionService.GetGroupSessionsAsync(userId, group.GroupReservationId);

        var participant = Assert.Single(result.Participants);
        Assert.Equal(session.Id, participant.Id);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupSessionsAsync_SessionsWithDifferentSubmitState_PassesIsSubmittedThrough(
        string userId, GroupReservation group, Reservation reservation)
    {
        SetupOwnedGroup(group, reservation, userId);
        var submitted = CreateSession("session-submitted", group.GroupReservationId, isSubmitted: true);
        var draft = CreateSession("session-draft", group.GroupReservationId, isSubmitted: false);
        SetupSessions(group.GroupReservationId, submitted, draft);

        var result = await _guestSessionService.GetGroupSessionsAsync(userId, group.GroupReservationId);

        Assert.Collection(result.Participants,
            p => Assert.True(p.IsSubmitted),
            p => Assert.False(p.IsSubmitted));
    }

    [Theory]
    [AutoData]
    public async Task DeleteGuestSessionAsync_UserIsNotReservationOwner_ThrowsActionForbiddenException(
        string userId, string otherCustomerId, string guestSessionId, GroupReservation group, Reservation reservation)
    {
        SetupOwnedGroup(group, reservation, otherCustomerId);

        await Assert.ThrowsAsync<ActionForbiddenException>(
            () => _guestSessionService.DeleteGuestSessionAsync(userId, group.GroupReservationId, guestSessionId));

        _mockGuestSessionRepository.Verify(r => r.DeleteAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Theory]
    [AutoData]
    public async Task DeleteGuestSessionAsync_GroupNotFound_ThrowsGroupReservationNotFoundException(
        string userId, string groupReservationId, string guestSessionId)
    {
        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(
            () => _guestSessionService.DeleteGuestSessionAsync(userId, groupReservationId, guestSessionId));

        _mockGuestSessionRepository.Verify(r => r.DeleteAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Theory]
    [AutoData]
    public async Task DeleteGuestSessionAsync_SessionNotFound_ThrowsGuestSessionNotFoundException(
        string userId, string guestSessionId, GroupReservation group, Reservation reservation)
    {
        SetupOwnedGroup(group, reservation, userId);
        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSessionId))
            .ReturnsAsync((GuestSession?)null);

        await Assert.ThrowsAsync<GuestSessionNotFoundException>(
            () => _guestSessionService.DeleteGuestSessionAsync(userId, group.GroupReservationId, guestSessionId));

        _mockGuestSessionRepository.Verify(r => r.DeleteAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Theory]
    [AutoData]
    public async Task DeleteGuestSessionAsync_SessionFromAnotherGroup_ThrowsValidationException(
        string userId, string otherGroupId, GroupReservation group, Reservation reservation)
    {
        SetupOwnedGroup(group, reservation, userId);
        var session = CreateSession("session-1", otherGroupId);
        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(session.Id))
            .ReturnsAsync(session);

        await Assert.ThrowsAsync<ValidationException>(
            () => _guestSessionService.DeleteGuestSessionAsync(userId, group.GroupReservationId, session.Id));

        _mockGuestSessionRepository.Verify(r => r.DeleteAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Theory]
    [AutoData]
    public async Task DeleteGuestSessionAsync_ValidRequest_DeletesSessionAndReturnsRemainingWithTotals(
        string userId, GroupReservation group, Reservation reservation)
    {
        const decimal price = 40m;
        SetupOwnedGroup(group, reservation, userId);
        var dish = CreateDish("dish-1", price);
        var toDelete = CreateSession("session-deleted", group.GroupReservationId, items: [CreateItem(dish.Id, 5)]);
        var remaining1 = CreateSession("session-1", group.GroupReservationId, items: [CreateItem(dish.Id, 1)]);
        var remaining2 = CreateSession("session-2", group.GroupReservationId, items: [CreateItem(dish.Id, 3)]);
        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(toDelete.Id))
            .ReturnsAsync(toDelete);
        SetupSessions(group.GroupReservationId, remaining1, remaining2);
        SetupDishes(dish);

        var result = await _guestSessionService.DeleteGuestSessionAsync(userId, group.GroupReservationId, toDelete.Id);

        _mockGuestSessionRepository.Verify(r => r.DeleteAsync(toDelete), Times.Once);
        Assert.Collection(result.Participants,
            p =>
            {
                Assert.Equal(remaining1.Id, p.Id);
                Assert.Equal(price, p.Total);
            },
            p =>
            {
                Assert.Equal(remaining2.Id, p.Id);
                Assert.Equal(3 * price, p.Total);
            });
        Assert.Equal(4 * price, result.Total);
    }

    [Theory]
    [AutoData]
    public async Task DeleteGuestSessionAsync_RemainingSessionsIncludeOwner_ReturnsOwnerFirstTypedOwner(
        string userId, GroupReservation group, Reservation reservation)
    {
        SetupOwnedGroup(group, reservation, userId);
        var toDelete = CreateSession("session-deleted", group.GroupReservationId);
        var guest = CreateSession("guest-1", group.GroupReservationId);
        var owner = CreateSession("owner", group.GroupReservationId, isOwner: true);
        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(toDelete.Id))
            .ReturnsAsync(toDelete);
        SetupSessions(group.GroupReservationId, guest, owner);

        var result = await _guestSessionService.DeleteGuestSessionAsync(userId, group.GroupReservationId, toDelete.Id);

        Assert.Collection(result.Participants,
            p =>
            {
                Assert.Equal(owner.Id, p.Id);
                Assert.Equal(ParticipantType.Owner, p.Type);
            },
            p =>
            {
                Assert.Equal(guest.Id, p.Id);
                Assert.Equal(ParticipantType.Guest, p.Type);
            });
    }

    [Theory]
    [AutoData]
    public async Task DeleteGuestSessionAsync_RemainingSessionHasMissingDish_SkipsItemInsteadOfThrowing(
        string userId, GroupReservation group, Reservation reservation)
    {
        const decimal price = 15m;
        SetupOwnedGroup(group, reservation, userId);
        var dish = CreateDish("dish-existing", price);
        var toDelete = CreateSession("session-deleted", group.GroupReservationId);
        var remaining = CreateSession("session-1", group.GroupReservationId,
            items: [CreateItem("dish-deleted", 2), CreateItem(dish.Id, 2)]);
        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(toDelete.Id))
            .ReturnsAsync(toDelete);
        SetupSessions(group.GroupReservationId, remaining);
        SetupDishes(dish);

        var result = await _guestSessionService.DeleteGuestSessionAsync(userId, group.GroupReservationId, toDelete.Id);

        var participant = Assert.Single(result.Participants);
        var item = Assert.Single(participant.Items);
        Assert.Equal(dish.Id, item.Dish.Id);
        Assert.Equal(2 * price, participant.Total);
        Assert.Equal(2 * price, result.Total);
    }

    private void SetupOwnedGroup(GroupReservation group, Reservation reservation, string customerId)
    {
        group.ReservationId = reservation.Id;
        reservation.CustomerId = customerId;

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(group.GroupReservationId))
            .ReturnsAsync(group);
        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);
    }

    private void SetupSessions(string groupReservationId, params GuestSession[] sessions)
    {
        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservationId))
            .ReturnsAsync(sessions.ToList());
    }

    private void SetupDishes(params Dish[] dishes)
    {
        _mockDishRepository
            .Setup(r => r.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(dishes);
    }

    private static GuestSession CreateSession(
        string id, string groupReservationId, bool isOwner = false, bool isSubmitted = false, List<OrderItem>? items = null) => new()
    {
        Id = id,
        GroupReservationId = groupReservationId,
        GuestName = $"name-{id}",
        Dishes = items ?? [],
        IsOwnerSession = isOwner,
        IsSubmitted = isSubmitted,
    };

    private static Dish CreateDish(string id, decimal price) => new()
    {
        Id = id,
        Name = $"name-{id}",
        Price = price,
        State = DishState.Available,
    };

    private static OrderItem CreateItem(string dishId, int quantity) => new()
    {
        DishId = dishId,
        Quantity = quantity,
        State = OrderItemState.Pending,
    };
}





