var app = angular.module('myApp', ['ngRoute']);

app.config(function($routeProvider, $locationProvider) {
  // Keep #! style (matches your $location.path usages like '#!/login')
  $locationProvider.hashPrefix('!');

  $routeProvider
    .when("/", {
      templateUrl: "home.html",
      controller: "mainController"
    })
    

    .when("/professional", {
      templateUrl: "profesional.html",
      controller: "mainController"
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
    // Initialize Bootstrap dropdowns after route change
    $timeout(function() {
      var dropdownElementList = [].slice.call(document.querySelectorAll('.dropdown-toggle'))
      dropdownElementList.map(function (dropdownToggleEl) {
        return new bootstrap.Dropdown(dropdownToggleEl)
      });
    }, 0);
  });
});

app.controller('mainController', function ($scope, $http, $location) {
    $scope.message = false;
});




