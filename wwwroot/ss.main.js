var app = angular.module('myApp', ['ngRoute']);

/* 🔹 tiny shared store to keep the selected service across routes */
app.factory('Selection', function () {
  var data = { service: null };
  return {
    setService: function (s) { data.service = s; },
    getService: function () { return data.service; },
    clear: function () { data.service = null; }
  };
});

app.config(function($routeProvider, $locationProvider) {
  $locationProvider.hashPrefix('!');

  $routeProvider
    .when("/",            { templateUrl: "home.html",        controller: "mainController" })
    .when("/login",       { templateUrl: "login.html",       controller: "logincontroller" })
    .when("/services/:serviceType", { templateUrl: "service.html", controller: "servicecontroller" })
    .when("/register",    { templateUrl: "register.html",    controller: "mainController" })
    .when("/professional",{ templateUrl: "profesional.html", controller: "mainController" })
    .when("/service-detail",{ templateUrl: "servicedetail.html", controller: "servicecontroller" })
    .otherwise({ redirectTo: "/" });
});

app.run(function($rootScope, $timeout){
  $rootScope.$on('$routeChangeSuccess', function(){
    if (window.AOS) {
      try { AOS.refreshHard(); } catch(e) {}
      $timeout(function(){ try { AOS.refreshHard(); } catch(e) {} }, 0);
    }
    $timeout(function() {
      var dropdowns = [].slice.call(document.querySelectorAll('.dropdown-toggle'));
      dropdowns.map(function (el) { return new bootstrap.Dropdown(el); });
    }, 0);
  });
});

app.controller('mainController', function ($scope, $http, $location, $rootScope) {
  $scope.message = false;
  $scope.swapAuth = false;
  $scope.reg   = { FullName:'', Email:'', Phone:'', PasswordHash:'', confirm:'' };
  $scope.signin= { Email:'', PasswordHash:'' };
  $scope.otp   = { Code:'' };
  $scope.otpSent = false;

  $scope.professional = { CompanyName:'', Address:'', CategoryIds:[] };
  $scope.loggedInUser = null;

  $scope.getCategories = function () {
    $http.get("/api/data/categories")
      .then(function (res) {
        $scope.categories = res.data;
        if (typeof res.data === "string" && res.data.indexOf("<!DOCTYPE html>") === 0) {
          console.error("API returned HTML (SPA fallback). Check /api/* routing.");
          $scope.categories = [];
        }
      }, function (err) {
        console.error("Error loading categories:", err);
        $scope.categories = [];
      });
  };
  $scope.getCategories();

  $scope.signup = function(form){
    $http.post('/api/data/register', angular.toJson($scope.reg), {
      headers: { 'Content-Type': 'application/json; charset=utf-8' }
    }).then(function (res) {
      if(res.data && res.data.message){
        $scope.status = { ok: true, err: false, msg: res.data.message };
        $scope.otpSent = true;
      } else {
        $scope.status = { ok: false, err: true, msg: 'Registration failed ❌' };
        alert($scope.status.msg);
      }
    }, function (error) {
      var msg = (error && error.data) || 'Registration failed ❌';
      $scope.status = { ok: false, err: true, msg: msg };
      alert($scope.status.msg);
    });
  };

  $scope.verifyOtp = function(form){
    $http.post('/api/data/verify-otp', angular.toJson({ Email: $scope.reg.Email, Otp: $scope.otp.Code }), {
      headers: { 'Content-Type': 'application/json; charset=utf-8' }
    }).then(function (res) {
      if(res.data && res.data.message){
        $scope.status = { ok: true, err: false, msg: res.data.message };
        alert($scope.status.msg);
        $location.path('/login');
      } else {
        $scope.status = { ok: false, err: true, msg: 'Verification failed ❌' };
        alert($scope.status.msg);
      }
    }, function (error) {
      var msg = (error && error.data) || 'Verification failed ❌';
      $scope.status = { ok: false, err: true, msg: msg };
      alert($scope.status.msg);
    });
  };

  $scope.resendOtp = function(){
    $http.post('/api/data/register', angular.toJson($scope.reg), {
      headers: { 'Content-Type': 'application/json; charset=utf-8' }
    }).then(function (res) {
      $scope.status = { ok: true, err: false, msg: 'OTP resent to your email.' };
      alert($scope.status.msg);
    }, function (error) {
      var msg = (error && error.data) || 'Failed to resend OTP ❌';
      $scope.status = { ok: false, err: true, msg: msg };
      alert($scope.status.msg);
    });
  };

  $scope.signinUser = function(form){
    $http.post('/api/data/login', angular.toJson($scope.signin), {
      headers: { 'Content-Type': 'application/json; charset=utf-8' }
    }).then(function (res) {
      if(res.data && res.data.success){
        $rootScope.loggedInUser = res.data.user;
        $scope.status = { ok: true, err: false, msg: 'Login successful ✔' };
        alert($scope.status.msg);
        $location.path('/login');
      } else {
        $scope.status = { ok: false, err: true, msg: 'Login failed ❌' };
        alert($scope.status.msg);
      }
    }, function (error) {
      var msg = (error && error.data) || 'Login failed ❌';
      $scope.status = { ok: false, err: true, msg: msg };
      alert($scope.status.msg);
    });
  };

  $scope.swap = function(id,containerSelector, a){
    $scope.swapAuth=a;
    var el = document.getElementById(id);
    if (!el) return;
    if (containerSelector) {
      var existing = document.querySelectorAll(containerSelector + ' .active');
      Array.prototype.forEach.call(existing, function(n){ n.classList.remove('active'); });
    }
    el.classList.add('active');
  };

  $scope.book = function(){ $location.path('/login'); };

  $scope.saveProfessional = function(form){
    if (!$scope.loggedInUser) { alert('Please log in first.'); return; }
    var data = {
      UserId: $scope.loggedInUser.UserID,
      CompanyName: $scope.professional.CompanyName,
      Address: $scope.professional.Address,
      CategoryIds: $scope.professional.CategoryIds
    };
    $http.post('/api/data/professionals', angular.toJson(data), {
      headers: { 'Content-Type': 'application/json; charset=utf-8' }
    }).then(function (res) {
      var ok = res.data && res.data.message;
      $scope.status = { ok: !!ok, err: !ok, msg: ok ? res.data.message : 'Save failed ❌' };
      alert($scope.status.msg);
    }, function (error) {
      var msg = (error && error.data) || 'Save failed ❌';
      $scope.status = { ok: false, err: true, msg: msg };
      alert($scope.status.msg);
    });
  };
});

app.controller('logincontroller', function ($scope, $location, $http) {
  $scope.goToCategory = function (category) {
    $location.path('/services/' + category);
  };

  $scope.getCategories = function () {
    $http.get("/api/data/categories")
      .then(function (res) {
        $scope.categories = res.data;
        if (typeof res.data === "string" && res.data.indexOf("<!DOCTYPE html>") === 0) {
          console.error("API returned HTML (SPA fallback). Check /api/* routing.");
          $scope.categories = [];
        }
      }, function (err) {
        console.error("Error loading categories:", err);
        $scope.categories = [];
      });
  };
  $scope.getCategories();

  $scope.viewProfile = function() { alert("View Profile clicked - feature to be implemented."); };
  $scope.signOut    = function() { $location.path('/'); };
});

/* -------------------- service list + detail -------------------- */
app.controller('servicecontroller', function ($scope, $location, $routeParams, $http, Selection, $rootScope, $timeout) {
  $scope.serviceType = $routeParams.serviceType;
  $scope.showProfessionalList = true;
  $scope.selectedService = null;
  $scope.total = 0;
  console.log($rootScope.loggedInUser);
  $scope.loggedInUser = $rootScope.loggedInUser;
  console.log($scope.loggedInUser);
  $scope.cust = {
        name: $scope.loggedInUser.FullName,
        email: $scope.loggedInUser.Email,
        phone: $scope.loggedInUser.Phone,
        street: '',
        suite: '',
        city: '',
        state: '',
        zip: ''
      };
  console.log($scope.cust);

  function normalizeService(s) {
    // handle PascalCase or camelCase from API
    return {
      serviceID:       s.serviceID       || s.ServiceID,
      professionalID:  s.professionalID  || s.ProfessionalID,
      categoryID:      s.categoryID      || s.CategoryID,
      serviceName:     s.serviceName     || s.ServiceName,
      title:           s.title           || s.Title,
      price:           s.price           || s.Price,
      estimatedHours:  s.estimatedHours  || s.EstimatedHours,
      description:     s.description     || s.Description,
      isActive: (typeof s.isActive !== 'undefined') ? s.isActive : s.IsActive
    };
  }

  $scope.getCategories = function () {
    $http.get("/api/data/categories").then(function (res) {
      $scope.categories = res.data;
    }, function (err) {
      console.error("Error loading categories:", err);
      $scope.categories = [];
    });
  };

  $scope.back = function () {
    $scope.showProfessionalList = true;
    $scope.services = [];
  };

  $scope.loadProfessionals = function() {
    var categoryId = $scope.serviceType;
    if (categoryId) {
      $http.get('/api/data/professionals?categoryId=' + categoryId)
        .then(function(res) {
          $scope.professionals = res.data;
        }, function(err) {
          console.error('Error loading professionals:', err);
          $scope.professionals = [];
        });
    }
  };

  // Save selection and navigate to details
  $scope.selectedservice = function(service){
    var normalized = normalizeService(service);
    Selection.setService(normalized);
    $location.path('/service-detail');
  };

  // If we are on detail page, restore selection
  if ($location.path() === '/service-detail') {
    var sel = Selection.getService();
    if (!sel) {
      // no selection – send user back to the list
      $location.path('/services/' + ($scope.serviceType || ''));
      return;
    }
    $scope.selectedService = sel;
    $scope.total = sel.price || 0;
    // Populate customer details from logged-in user
    if ($rootScope.loggedInUser) {
      $timeout(function() {
        $scope.cust = {
          name: $rootScope.loggedInUser.FullName,
          email: $rootScope.loggedInUser.Email,
          phone: $rootScope.loggedInUser.Phone,
          street: '',
          suite: '',
          city: '',
          state: '',
          zip: ''
        };
      }, 0);
    }
  } else {
    // only load on list page
    $scope.getCategories();
    $scope.loadProfessionals();
  }

  $scope.selectProfessional = function(pro) {
    $scope.selectedProfessional = pro;
    $scope.loadServices(pro.ProfessionalID || pro.professionalID);
  };

  $scope.loadServices = function(professionalId) {
    $http.get('/api/data/services?professionalId=' + professionalId)
      .then(function(res) {
        $scope.services = res.data;
        $scope.showProfessionalList = false;
      }, function(err) {
        console.error('Error loading services:', err);
        $scope.services = [];
      });
  };
  $scope.submitOrder = function() {
    if (!$scope.loggedInUser) { alert('Please log in first.'); return; }  
    if (!$scope.selectedService) { alert('Please select a service.'); return; }
    if (!$scope.selectedProfessional) { alert('Please select a professional.'); return; }   
    var orderData = {
      UserId: $scope.loggedInUser.UserID,
      ProfessionalId: $scope.selectedProfessional.ProfessionalID || $scope.selectedProfessional.professionalID, 
      ServiceId: $scope.selectedService.ServiceID || $scope.selectedService.serviceID,
      ScheduledDate: $scope.scheduledDate,
      CustomerName: $scope.cust.name,
      CustomerEmail: $scope.cust.email, 
      CustomerPhone: $scope.cust.phone,
      AddressStreet: $scope.cust.street,
      AddressSuite: $scope.cust.suite,  
      AddressCity: $scope.cust.city,
      AddressState: $scope.cust.state,
      AddressZip: $scope.cust.zip,
      TotalPrice: $scope.total
    };  
    $http.post('/api/data/orders', angular.toJson(orderData), {
      headers: { 'Content-Type': 'application/json; charset=utf-8' }
    }).then(function (res) {
      var ok = res.data && res.data.message;  
      $scope.status = { ok: !!ok, err: !ok, msg: ok ? res.data.message : 'Order submission failed ❌' };
      alert($scope.status.msg);   
      if (ok) {

        // Clear selection and navigate back to services list
        Selection.clear();
        $location.path('/services/' + ($scope.serviceType || ''));
      } 
    }, function (error) {

      var msg = (error && error.data) || 'Order submission failed ❌';
      $scope.status = { ok: false, err: true, msg: msg };
      alert($scope.status.msg);
    });

  };
});

