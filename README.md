### SOFTWARE DESIGN CONCEPTS

---

## 1) Abstraction

Abstraction is applied using interfaces to define behaviors without exposing implementation details.

# Evidence

- `/Abstractions/UserCreator.cs`
- `/Interfaces/ICartService.cs`
- `/Interfaces/ICustomerContext.cs`
- `/Interfaces/INotificationService.cs`
- `/Interfaces/IOrderService.cs`
- `/Interfaces/IProductService.cs`
- `/Interfaces/IProfileImageStorage.cs`
- `/Interfaces/IProfileService.cs`
- `/Interfaces/IReviewService.cs`
- `/Interfaces/IUserService.cs`

---

## 2) Modularity

System is divided into independent modules (Controllers, Services, Interfaces, Models, Factories, Observers).

# Evidence

- `/Controllers/` → Handles HTTP requests
- `/Services/` → Contains business logic
- `/Interfaces/` → Defines contracts
- `/Models/` → Data representation
- `/Factories/` → Object creation
- `/Observers/` → Event handling

---

## 3) Encapsulation

Internal logic is hidden inside service classes and models to prevent direct access from controllers.

# Evidence

- `/Models/Order.cs` → Private `_observers` list
- `/Services/CartService.cs`
- `/Services/CustomerContext.cs`
- `/Services/NotificationService.cs`
- `/Services/OrderService.cs` → Encapsulates transaction + stock logic
- `/Services/ProductService.cs`
- `/Services/ProfileService.cs` → Hides file storage logic

---

## 4) Functional Independence

Each service performs a single, independent function.

# Evidence

- `/Services/CartService.cs` → Only handles cart operations
- `/Services/CustomerContext.cs` → Current user resolution
- `/Services/NotificationService.cs` → Only handles notifications
- `/Services/OrderService.cs` → Only handles orders
- `/Services/ProductService.cs` → Only handles product logic
- `/Services/ProfileService.cs` → Profile management

---

## 5) Refinement

Complex logic is broken down into simpler components.

# Evidence

- `/Controllers/CustomerController.cs` → delegates business logic to services
- `/Controllers/AuthController.cs` → delegates authentication logic
- `/Services/OrderService.cs` → separates order grouping, stock deduction, and notification logic
- `/Services/CartService.cs` → separates cart CRUD operations

---

## 6) Refactoring

Business logic was moved from controllers into service layer to reduce controller complexity.

# Evidence

- `/Controllers/CustomerController.cs` → Reduced complexity
- `/Controllers/AuthController.cs` → Thin design
- Logic migrated to `/Services/`

---

## 7) Architecture

Layered architecture is implemented:

# Evidence

- `/Controllers/` → Presentation Layer
- `/Services/` → Business Logic Layer
- `/Interfaces/` → Abstraction Layer
- `/Factories/` → Design Pattern Layer
- `/Observers/` → Design Pattern Layer
- `/Data/AppDbContext.cs` → Data Access Layer

--------------------------------------------

### SOLID Principles

---

## 1) Single Responsibility Principle (SRP)

Each class has only one responsibility.

# Evidence

- `/Controllers/AuthController.cs` → Handles authentication UI only
- `/Controllers/CustomerController.cs` → Customer requests handling
- `/Services/CartService.cs` → Handles cart logic only
- `/Services/NotificationService.cs` → Notification handling only
- `/Services/OrderService.cs` → Order processing only
- `/Services/ProductService.cs` → Product retrieval only

---

## 2) Open/Closed Principle (OCP)

Software should be open for extension, but closed for modification.

# Evidence

## Open for Extension (can add new types without modifying existing code)
- `/Factories/CustomerCreator.cs`
- `/Factories/CustomerServiceCreator.cs`
- `/Factories/SellerCreator.cs`

## Closed for Modification (existing logic is not changed when extending)
- `/Abstractions/UserCreator.cs` → does not change when new user types are added
- `/Controllers/AuthController.cs` → does not change when new user roles are added

---

## 3) Liskov Substitution Principle (LSP)

Subclasses should be able to replace their parent class without breaking the program.

# Evidence

## Base class:
- `/Models/User.cs`

## Derived classes:

- `/Models/Admin.cs`
- `/Models/Customer.cs`
- `/Models/CustomerService.cs`
- `/Models/Seller.cs`

---

## 4) Interface Segregation Principle (ISP)

Clients should not be forced to depend on methods they do not use.

# Evidence

- `/Interfaces/ICartService.cs`
- `/Interfaces/INotificationService.cs`
- `/Interfaces/IOrderService.cs`
- `/Interfaces/IProductService.cs`
- `/Interfaces/IProfileService.cs`
- `/Interfaces/IReviewService.cs`
- `/Interfaces/IUserService.cs`

---

## 5) Dependency Inversion Principle (DIP)

High-level modules should not depend on low-level modules. Both should depend on abstractions, not on concrete implementations.

# Evidence

## High-Level Modules
- `/Controllers/AuthController.cs`
- `/Controllers/CustomerController.cs`

## Low-Level Modules (Implementations)
- `/Services/CartService.cs`
- `/Services/NotificationService.cs`
- `/Services/OrderService.cs`
- `/Services/ProductService.cs`
- `/Services/ProfileService.cs`
- `/Services/UserService.cs`
- `/Factories/CustomerCreator.cs`
- `/Factories/CustomerServiceCreator.cs`
- `/Factories/SellerCreator.cs`
- `/Observers/AdminNotificationObserver.cs`
- `/Observers/CustomerNotificationObserver.cs`
- `/Observers/SellerNotificationObserver.cs`

## Abstractions
- `/Abstractions/UserCreator.cs`
- `/Interfaces/ICartService.cs`
- `/Interfaces/INotificationService.cs`
- `/Interfaces/IOrderService.cs`
- `/Interfaces/IProductService.cs`
- `/Interfaces/IProfileService.cs`
- `/Interfaces/IUserService.cs`
- `/Interfaces/OrderStatusObserver.cs`
- `/Interfaces/OrderStatusSubject.cs`

--------------------------------------------

### Design Patterns

## Factory Method Pattern

Used to create different user types.

# Evidence

- `/Abstractions/UserCreator.cs`
- `/Factories/CustomerCreator.cs`
- `/Factories/CustomerServiceCreator.cs`
- `/Factories/SellerCreator.cs`

---

## Observer Pattern

Used to notify components when order status changes.

# Evidence

- `/Interfaces/OrderStatusObserver.cs`
- `/Interfaces/OrderStatusSubject.cs`
- `/Observers/AdminNotificationObserver.cs`
- `/Observers/CustomerNotificationObserver.cs`
- `/Observers/NotificationObserver.cs`

---

## Strategy Pattern



# Evidence



---