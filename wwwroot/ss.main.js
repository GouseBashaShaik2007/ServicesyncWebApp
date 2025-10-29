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
    .when("/orders", { templateUrl: "orders.html", controller: "logincontroller" })
    .when("/viewprofile", { templateUrl: "viewprofile.html", controller: "logincontroller" })
    .when("/register",    { templateUrl: "register.html",    controller: "mainController" })
   // .when("/professional",{ templateUrl: "profesional.html", controller: "mainController" })
    .when("/pllogin",{ templateUrl: "professionallogin.html", controller: "mainController" })
    .when("/professional/:ProID",{ templateUrl: "profesional.html", controller: "mainController" })
    .when("/service-detail",{ templateUrl: "servicedetail.html", controller: "servicecontroller" })
    .otherwise({ redirectTo: "/" });
});

app.run(function($rootScope, $timeout){
  // Load loggedInUser from localStorage on app start
  var storedUser = localStorage.getItem('loggedInUser');
  if (storedUser) {
    try {
      $rootScope.loggedInUser = JSON.parse(storedUser);
    } catch(e) {
      console.error('Failed to parse loggedInUser from localStorage', e);
      localStorage.removeItem('loggedInUser');
    }
  }

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

app.controller('mainController', function ($scope, $location, $routeParams, $http, Selection, $rootScope, $timeout) {
  $scope.message = false;
  $scope.swapAuth = false;
  $scope.reg   = { FullName:'', Email:'', Phone:'', PasswordHash:'', confirm:'' };
  $scope.signin= { Email:'', PasswordHash:'' };
  $scope.otp   = { Code:'' };
  $scope.otpSent = false;
  $scope.ProID = $routeParams.ProID;

  $scope.professional = { CompanyName:'', Address:'', CategoryIds:[] };
  $scope.loggedInUser = null;

   // Professional enquiry and login
  $scope.newProfessional = {};
  $scope.signinProfessional = {};
  $scope.showNewForm = false;
  $scope.showSigninForm = false;
  $scope.enquirySubmitting = false;
  $scope.signinSubmitting = false;
  $scope.enquiryMessage = '';
  $scope.signinError = '';

  $scope.submitProfessionalEnquiry = function() {
    console.log($scope.newProfessional);
    $scope.enquirySubmitting = true;
    $scope.enquiryMessage = '';

    $http.post('/api/data/professional-enquiry', $scope.newProfessional)
      .then(function(response) {
        $scope.enquiryMessage = response.data.message;
        $scope.newProfessional = {};
        $scope.enquirySubmitting = false;
      })
      .catch(function(error) {
        console.error('Enquiry submission failed:', error);
        $scope.enquiryMessage = 'Submission failed. Please try again.';
        $scope.enquirySubmitting = false;
      });
  };

  $scope.professionalLogin = function() {
    $scope.signinSubmitting = true;
    $scope.signinError = '';

    $http.post('/api/data/professional-login', $scope.signinProfessional)
      .then(function(response) {
        if (response.data.success) {
          localStorage.setItem('professional', JSON.stringify(response.data.professional));
          $location.path('/professional/' + response.data.professional.professionalID);
        }
      })
      .catch(function(error) {
        console.error('Professional login failed:', error);
        $scope.signinError = error.data?.error || 'Login failed. Please check your credentials.';
        $scope.signinSubmitting = false;
      });
  };

  $scope.loadprofessionalorder = function(ProID) {
    console.log(ProID);
    $http.get('/api/data/getprofessionalorders?professionalId=' + ProID)
      .then(function (res) {
        $scope.professionalorders = res.data;
        if (typeof res.data === "string" && res.data.indexOf("<!DOCTYPE html>") === 0) {
          console.error("API returned HTML (SPA fallback). Check /api/* routing.");
          $scope.professionalorders = [];
        }
      }, function (err) {
        console.error("Error loading professional orders:", err);
        $scope.professionalorders = [];
      });
  };

  $scope.updateOrderStatus = function(orderId, newStatus) {
    $http.post('/api/data/updateorderstatus', { OrderID: orderId, OrderStatus: newStatus })
      .then(function (res) {
        alert('Order status updated successfully!');
        // Reload orders to reflect changes
        $scope.loadprofessionalorder($scope.ProID);
      }, function (err) {
        console.error("Error updating order status:", err);
        alert('Failed to update order status.');
      });
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
        // Save loggedInUser to localStorage for persistent login
        localStorage.setItem('loggedInUser', JSON.stringify(res.data.user));
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

app.controller('logincontroller', function ($scope, $location, $routeParams, $http, Selection, $rootScope, $timeout) {
  $scope.user = null;
  $scope.user = $rootScope.loggedInUser;


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
 // $scope.getCategories();

  $scope.getorders = function (user) {
    $scope.id=user
    console.log($scope.id.userID)
    $http.get('/api/data/getorders?userId=' + $scope.id.userID)
      .then(function (res) {
        $scope.orders = res.data;
        if (typeof res.data === "string" && res.data.indexOf("<!DOCTYPE html>") === 0) {
          console.error("API returned HTML (SPA fallback). Check /api/* routing.");
          $scope.orders = [];
        }
      }, function (err) {
        console.error("Error loading orders:", err);
        $scope.orders = [];
      });
  }
  $scope.submitReview = function(order,review){
   $scope.ratingdata = {};
    $scope.ratingdata.ReviewText=review.reviewtext;
    $scope.ratingdata.Rating=review.rating;
    $scope.ratingdata.CustomerID=order.userID;
    $scope.ratingdata.ProfessionalID=order.professionalID;
    $scope.ratingdata.OrderID=order.orderID;
    $scope.ratingdata.IsVerified = true;
    $scope.ratingdata.IsPublic = true;
    console.log($scope.ratingdata);

    $http.post('/api/data/submitreview', $scope.ratingdata, {
      headers: { 'Content-Type': 'application/json' }
    }).then(function (res) {
      var ok = res.data && res.data.message;
      $scope.status = { ok: !!ok, err: !ok, msg: ok ? res.data.message : 'Review submission failed ❌' };
      alert($scope.status.msg);
    }, function (error) {

      var msg = (error && error.data) || 'Review submission failed ❌';
      $scope.status = { ok: false, err: true, msg: msg };
      alert($scope.status.msg);
    });
  };

  $scope.viewprofile = function(user) {
     $scope.id=user
    console.log($scope.id.userID)
    $http.get('/api/data/getuserprofile?userId=' + $scope.id.userID)
      .then(function (res) {
        $scope.userprofile = res.data;
        if (typeof res.data === "string" && res.data.indexOf("<!DOCTYPE html>") === 0) {
          console.error("API returned HTML (SPA fallback). Check /api/* routing.");
          $scope.userprofile = [];
        }
      }, function (err) {
        console.error("Error loading user profile:", err);
        $scope.userprofile = [];
      });
   };

  $scope.signOut    = function() {
    // Clear loggedInUser from localStorage and $rootScope on logout
    localStorage.removeItem('loggedInUser');
    localStorage.removeItem('professional');
    if (window.angular) {
      var $rootScope = angular.element(document.body).injector().get('$rootScope');
      $rootScope.$apply(function() {
        $rootScope.loggedInUser = null;
      });
    }
    $location.path('/');
  };

 
});

/* -------------------- service list + detail -------------------- */
app.controller('servicecontroller', function ($scope, $location, $routeParams, $http, Selection, $rootScope, $timeout) {
  $scope.serviceType = $routeParams.serviceType;
  $scope.showProfessionalList = true;
  $scope.selectedService = null;
  $scope.total = 0;
  $scope.user = null;
  $scope.user = $rootScope.loggedInUser;
  console.log($scope.user);
   $scope.cust = {};
  $scope.cust = {
        name: $scope.user.fullName,
        email: $scope.user.email,
        phone: $scope.user.phone,
        street: '',
        suite: '',
        city: '',
        state: '',
        zip: '',
        Userid: $scope.user.userID
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

  function normalizeProfessional(p) {
    // handle PascalCase or camelCase from API
    return {
      professionalID: p.professionalID || p.ProfessionalID,
      companyName: p.companyName || p.CompanyName,
      email: p.email || p.Email,
      phone: p.phone || p.Phone,
      address1: p.address1 || p.Address1,
      address2: p.address2 || p.Address2,
      city: p.city || p.City,
      state: p.state || p.State,
      postalCode: p.postalCode || p.PostalCode,
      ratings: p.ratings || p.Ratings
    };
  }

  $scope.loadProfessionals = function() {
    var categoryId = $scope.serviceType;
    if (categoryId) {
      $http.get('/api/data/professionals?categoryId=' + categoryId)
        .then(function(res) {
          $scope.professionals = res.data.map(normalizeProfessional);
          console.log($scope.professionals);
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

  $scope.getreviews = function(professionalId) {
    $http.get('/api/data/reviews?professionalId=' + professionalId)
      .then(function(res) {
        $scope.reviews = res.data;
        $('#reviewsModal').modal('show');
      }, function(err) {
        console.error('Error loading reviews:', err);
        $scope.reviews = [];
      });
  };

  $scope.toggleService = function(s) {
    s.selected = !s.selected;
    var total = 0;
    ($scope.services || []).forEach(function(svc) {
      if (svc.selected) {
        total += svc.price || 0;
      }
    });
    $scope.total = total;
  };

  $scope.submitOrder = async function() {
    const datePart = new Date($scope.orderDate); // e.g. 2025-10-08
    const timePart = new Date($scope.orderTime); // e.g. 1970-01-01T05:48:00Z
    datePart.setHours(timePart.getHours(), timePart.getMinutes(), 0, 0);
    const scheduledStart = datePart.toISOString(); // "2025-10-08T05:48:00.000Z"

    // Handle payment first
    // if (typeof handlePaymentAndOrder === 'function') {
    //   var paymentSuccess = await handlePaymentAndOrder();
    //   if (!paymentSuccess) {
    //     return; // Payment failed, do not proceed with order
    //   }
    // }
    var orderData = {
      UserID: $scope.user.UserID || 16,
      ProfessionalID: 1,//$scope.selectedProfessional.professionalID || $scope.selectedProfessional.ProfessionalID,
      CategoryID: 1,//$scope.selectedService.categoryID || $scope.selectedService.CategoryID,
      ServiceAddress1: $scope.cust.street,
      ServiceAddress2: $scope.cust.suite ? $scope.cust.suite : null,
      City: $scope.cust.city,
      State: $scope.cust.state,
      PostalCode: $scope.cust.zip,
      ScheduledStart: scheduledStart,
      ScheduledEnd: null,
      Notes: $scope.orderNotes ? $scope.orderNotes : null,
      Subtotal: $scope.total,
      TaxAmount: 0,
      DiscountAmount: 0,
      PaymentStatus: 0, // Assuming 0 = pending
      OrderStatus: 0,   // Assuming 0 = new
      IsActive: true
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
        $location.path('/orders');
      }
    }, function (error) {

      var msg = (error && error.data) || 'Order submission failed ❌';
      $scope.status = { ok: false, err: true, msg: msg };
      alert($scope.status.msg);
    });

  };
});
