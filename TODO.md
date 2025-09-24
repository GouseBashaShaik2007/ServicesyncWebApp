# TODO: Professional and Service Management

## Backend Changes
- [x] Create Professional model (Models/Professional.cs)
- [x] Create ProfessionalService model (Models/ProfessionalService.cs)
- [x] Create Service model (Models/Service.cs)
- [x] Add GET /api/data/professionals?categoryId=X endpoint to get professionals by category
- [x] Add GET /api/data/services?professionalId=X endpoint to get services by professional
- [x] Update DataController.cs with new records and endpoints

## Frontend Changes
- [x] Update servicecontroller in ss.main.js to load professionals and services
- [x] Modify service.html to show professionals first, then services when professional selected
- [x] Add selectProfessional function to load services for selected professional
- [x] Update UI flow: Category -> Professionals -> Services

## Database Schema
- [x] Professionals table (ProfessionalID, CompanyName, Email, Phone, Address1, Address2, City, State, PostalCode, PasswordHash, PasswordSalt, Iterations, CreatedAt, UpdatedAt)
- [x] ProfessionalServices table (ProfessionalID, CategoryID, Rate, Description)
- [x] Services table (ServiceID, ProfessionalID, CategoryID, ServiceName, Title, Price, EstimatedHours, Description, IsActive, CreatedAt)

## Testing
- [ ] Test the new flow: Login -> Select Category -> View Professionals -> Select Professional -> View Services
- [ ] Verify API endpoints return correct data
- [ ] Test error handling for empty results

## Next Steps
- [ ] Implement professional registration and login (separate from user login)
- [ ] Add professional dashboard for managing services
- [ ] Implement booking/cart functionality for selected services
