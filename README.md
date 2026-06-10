### SOLID Principles

---

## 1) Single Responsibility Principle (SRP)

Each class has only one responsibility.

## Factory Method Pattern

Each creator class is responsible only for creating a specific user type.

# Evidence

- `/Abstractions/UserCreator.cs`
- `/Factories/CustomerCreator.cs`
- `/Factories/SellerCreator.cs`
- `/Factories/CustomerServiceCreator.cs`

## Observer Pattern

Each observer has only one notification responsibility.

# Evidence

- `/Observers/CustomerNotificationObserver.cs`
- `/Observers/SellerNotificationObserver.cs`
- `/Observers/AdminNotificationObserver.cs`

## Strategy Pattern

Each strategy handles one request-resolution behavior.

# Evidence

- `/Strategy/ActivateAccountStrategy.cs`
- `/Strategy/SuspendAccountStrategy.cs`
- `/Strategy/RefundStrategy.cs`
- `/Strategy/ReturnRefundStrategy.cs`

---

## 2) Open/Closed Principle (OCP)

Software should be open for extension, but closed for modification.

## Factory Method Pattern

New user types can be added by creating new creator classes without modifying existing creators.

# Evidence

## Open for Extension
- `/Factories/CustomerCreator.cs`
- `/Factories/CustomerServiceCreator.cs`
- `/Factories/SellerCreator.cs`

## Closed for Modification
- `/Abstractions/UserCreator.cs`

## Observer Pattern

New notification receivers can be added without modifying existing observers.

# Evidence

## Open for Extension
- `/Interfaces/OrderStatusObserver.cs`
- `/Observers/CustomerNotificationObserver.cs`
- `/Observers/SellerNotificationObserver.cs`
- `/Observers/AdminNotificationObserver.cs` 

## Closed for Modification
- `/Services/OrderStatusNotifier.cs` 

## Strategy Pattern

New request-solving behaviors can be introduced by creating new strategies.

# Evidence

## Open for Extension
- `/Strategy/ActivateAccountStrategy.cs`
- `/Strategy/SuspendAccountStrategy.cs`
- `/Strategy/RefundStrategy.cs`
- `/Strategy/ReturnRefundStrategy.cs`

## Closed for Modification
- `/Interfaces/IRequestStrategy.cs`

---

## 3) Liskov Substitution Principle (LSP)

Subclasses should be able to replace their parent class without breaking the program.

## Factory Method Pattern

All concrete creators can be used wherever UserCreator is expected.

# Evidence

## Base class:
- `/Abstractions/UserCreator.cs`

## Derived classes:

- `/Factories/CustomerCreator.cs`
- `/Factories/SellerCreator.cs`
- `/Factories/CustomerServiceCreator.cs`

---

## 4) Interface Segregation Principle (ISP)

Clients should not be forced to depend on methods they do not use.

## Observer Pattern

Observers only implement the notification operation they require.

# Evidence

- `/Interfaces/OrderStatusObserver.cs` → Each observer only implements the single update method.

## Strategy Pattern

Strategies only implement the request-solving behavior they require.

# Evidence

- `/Interfaces/IRequestStrategy.cs` → Each strategy only implements the solve operation.

---

## 5) Dependency Inversion Principle (DIP)

High-level modules should depend on abstractions rather than concrete implementations.

## Observer Pattern

# Evidence

## High-Level Modules
- `/Services/OrderStatusNotifier.cs`

## Low-Level Modules
- `/Observers/CustomerNotificationObserver.cs`
- `/Observers/SellerNotificationObserver.cs`
- `/Observers/AdminNotificationObserver.cs` 

## Abstractions
- `/Interfaces/OrderStatusObserver.cs`

--------------------------------------------

### Design Patterns

## Factory Method Pattern

Creates different user objects without exposing object creation logic to the client.

# Evidence

- `/Abstractions/UserCreator.cs`
- `/Factories/CustomerCreator.cs`
- `/Factories/CustomerServiceCreator.cs`
- `/Factories/SellerCreator.cs`

---

## Observer Pattern

Automatically notifies interested parties whenever an order status changes.

# Evidence

- `/Interfaces/OrderStatusObserver.cs`
- `/Interfaces/OrderStatusSubject.cs`
- `/Observers/OrderStatusNotifier.cs`
- `/Observers/CustomerNotificationObserver.cs`
- `/Observers/SellerNotificationObserver.cs`
- `/Observers/CustomerNotificationObserver.cs`
- `/Factories/OrderStatusNotifierFactory.cs`

---

## Strategy Pattern

Allows different request-resolution algorithms to be selected at runtime.

# Evidence

- `/Interfaces/IRequestStrategy.cs`
- `/Strategy/ActivateAccountStrategy.cs`
- `/Strategy/SuspendAccountStrategy.cs`
- `/Strategy/RefundStrategy.cs`
- `/Strategy/ReturnRefundStrategy.cs`

---