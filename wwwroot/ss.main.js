var app = angular.module('myApp', ['ngRoute']);

app.config(function($routeProvider, $locationProvider) {
  // Keep #! style (matches your $location.path usages like '#!/login')
  $locationProvider.hashPrefix('!');

  $routeProvider
    .when("/", {
      templateUrl: "home.html",
      controller: "mainController"
    })
    .when("/login", {
      templateUrl: "login.html",
      controller: "logincontroller"
    })
    .when("/services/:serviceType", {
      templateUrl: "service.html",
      controller: "servicecontroller"
    })
    .when('/register',{ templateUrl: 'register.html',controller: "mainController" })

    .when("/professional", {
      templateUrl: "profesional.html",
      controller: "mainController"
    })
    .when("/service-detail", {
      templateUrl: "servicedetail.html",
      controller: "servicecontroller"
    })
    .otherwise({ redirectTo: "/" });
});

app.run(function($rootScope, $timeout){
  // Re-run AOS after each route change so animations bind to new DOM
  $rootScope.$on('$routeChangeSuccess', function(){
    if (window.AOS) {
      try { AOS.refreshHard(); } catch(e) {}
      // small async to ensure DOM painted
      $timeout(function(){ try { AOS.refreshHard(); } catch(e) {} }, 0);
    }
  });
});

app.controller('mainController', function ($scope, $http, $location) {
  $scope.message = false;
  $scope.swapAuth=false;
   $scope.reg = { FullName:'', Email:'', Phone:'', PasswordHash:'', confirm:'' };
   $scope.signin = { Email:'', PasswordHash:'' };
  $scope.getCategories = function () {
    $http.get("/api/data/categories")
      .then(function (response) {
        $scope.categories = response.data;
        console.log($scope.categories)
        if (typeof response.data === "string" && response.data.indexOf("<!DOCTYPE html>") === 0) {
          console.error("API returned HTML (SPA fallback). Check server routing for /api/* vs index fallback.");
          $scope.categories = [];
        }
      }, function (error) {
        console.error("Error loading categories:", error);
        $scope.categories = [];
      });
  };
  $scope.getCategories();


$scope.signup = function(form){
  $http({
  method: 'POST',
  url: '/api/data/register',
  data: angular.toJson($scope.reg),
  headers: { 'Content-Type': 'application/json; charset=utf-8' }
})
  .then(function (response) {
   if(response.data && response.data.message){
    $scope.status = { ok: true, err: false, msg: 'Registration successful ✔' };
    alert($scope.status.msg);
    $location.path('/login');
   } else {
    $scope.status = { ok: false, err: true, msg: 'Registration failed ❌' };
    alert($scope.status.msg);
   }
  }, function (error) {
    // Handle error responses (like email already exists)
    if (error.data) {
      $scope.status = { ok: false, err: true, msg: error.data };
      alert($scope.status.msg);
    } else {
      $scope.status = { ok: false, err: true, msg: 'Registration failed ❌' };
      alert($scope.status.msg);
    }
  })
}

$scope.signinUser = function(form){
  console.log($scope.signin);
  $http({
  method: 'POST',
  url: '/api/data/login',
  data: angular.toJson($scope.signin),
  headers: { 'Content-Type': 'application/json; charset=utf-8' }
})
  .then(function (response) {
   if(response.data && response.data.success){
    $scope.status = { ok: true, err: false, msg: 'Login successful ✔' };
    alert($scope.status.msg);
    $location.path('/login');  // Redirect to home page after successful login
   } else {
    $scope.status = { ok: false, err: true, msg: 'Login failed ❌' };
    alert($scope.status.msg);
   }
  }, function (error) {
    // Handle login errors
    if (error.data) {
      $scope.status = { ok: false, err: true, msg: error.data };
      alert($scope.status.msg);
    } else {
      $scope.status = { ok: false, err: true, msg: 'Login failed ❌' };
      alert($scope.status.msg);
    }
  })
}

$scope.sendotp = function (data) {
  $http({
  method: 'POST',
  url: '/api/email/send',
  data: angular.toJson({
    to: 'gbasha951@gmail.com',
    subject: 'Testing...',
    html: '<p>Testing for the project otp is 858525</p>'
  }),
  headers: { 'Content-Type': 'application/json; charset=utf-8' }
})
  .then(function () {
    $scope.status = { ok: true, err: false, msg: 'Email sent ✔' };
  })
};


$scope.send = function () {
  $http({
  method: 'POST',
  url: '/api/email/send',
  data: angular.toJson({
    to: 'gbasha951@gmail.com',
    subject: 'Testing...',
    html: '<p>Testing for the project otp is 858525</p>'
  }),
  headers: { 'Content-Type': 'application/json; charset=utf-8' }
})
  .then(function () {
    $scope.status = { ok: true, err: false, msg: 'Email sent ✔' };
  })
};

//$scope.send();


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

  // $scope.showregistration  = function(){ $scope.showreg = true;  };
  // $scope.showregistration1 = function(){ $scope.showreg = false; };

  // //$scope.gotoregister = function () { $location.path('/professional'); };
  $scope.book= function(){ $location.path('/login'); };

  $scope.submitForm = function () {
    $http.post('/api/submit', $scope.formData).then(function () {
      $location.path('/submit');
    });
  };
});

app.controller('logincontroller', function ($scope, $location, $http) {
  $scope.goToCategory = function (a) {
     var m = String(a.name).trim().match(/^\S+/); // first non-space chunk
    $location.path('/services/' + m[0]);
  };

  $scope.getCategories = function () {
    $http.get("/api/data/categories")
      .then(function (response) {
        $scope.categories = response.data;
        console.log($scope.categories)
        if (typeof response.data === "string" && response.data.indexOf("<!DOCTYPE html>") === 0) {
          console.error("API returned HTML (SPA fallback). Check server routing for /api/* vs index fallback.");
          $scope.categories = [];
        }
      }, function (error) {
        console.error("Error loading categories:", error);
        $scope.categories = [];
      });
  };


});

app.controller('servicecontroller', function ($scope, $location, $routeParams) {
  // ---- ROUTE / STATE ----
  $scope.service = $routeParams.serviceType || 'home';
  $scope.selectedservicearr = [];         // original
  $scope.selectedServices = $scope.selectedservicearr; // alias for templates that use camelCase
  $scope.thankyou = false;
  $scope.thankYou = $scope.thankyou;      // alias
  $scope.total = 0;
  $scope.level = 1;
  $scope.step = 1;
  $scope.show = false; // initialize

  // ---- DATA ----
  $scope.list = [
    { title:'Presidential Cleaning Solutions', id:'home', image:'assets/img/house_ser.png', star:4, recent:67, index:1 },
    { title:'Flagship House Cleaning',        id:'home', image:'assets/img/house_ser.png', star:5, recent:94, index:2 },
    // ...
  ];

  $scope.Services = [
    { name:'Bathroom Cleaning', price:49, title:'Bathroom', service:'home', time:2, id:'home', hide:false },
    // ...
  ];

  // Optional alias if HTML expects lowercase "services"
  $scope.services = $scope.Services;

  // ---- UI FLOW ----
  $scope.selectlist = function () { $scope.step = 2; };
  $scope.checklevel = function () {
    $scope.step = 3;
   // $scope.gettotal();
  };

  // ---- SELECTION ----
  $scope.addservice = function (ser) {
    $scope.show = true;
    $scope.Services.forEach(function (item) {
      if (item.name === ser.name) item.hide = true;  // boolean!
    });
   // $scope.gettotal();
  };

  // $scope.remove = function (ser) {
  //   $scope.Services.forEach(function (item) {
  //     if (item.name === ser.name) item.hide = false;
  //   });
  //   $scope.gettotal();
  // };

  // ---- TOTALS ----
  $scope.gettotal = function () {
    $scope.selectedservicearr = $scope.Services.filter(function (s) { return s.hide === true; });
    $scope.selectedServices = $scope.selectedservicearr; // keep alias in sync

    var level = Number($scope.level) || 1;
    $scope.total = $scope.selectedservicearr.reduce(function (sum, s) {
      var p = Number(s.price) || 0;
      return sum + p * level;
    }, 0);
  };

  // ---- NAV ----
  $scope.showpayment   = function(a){ $scope.gettotal();
    $location.path('/service-detail'); };
  $scope.submitpayment = function(){ $scope.thankyou = true; $scope.thankYou = true; };
  $scope.home          = function(){ $location.path('/'); };
});
