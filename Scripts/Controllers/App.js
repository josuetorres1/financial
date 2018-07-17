'use strict';

var MYAPP = MYAPP || {};

MYAPP.namespace = function (nsString) {
    var parts = nsString.split('.'),
    parent = MYAPP,
    i;
    
    if (parts[0] === "MYAPP") {
        parts = parts.slice(1);
    }
    for (i = 0; i < parts.length; i += 1) {
        
        if (typeof parent[parts[i]] === "undefined") {
            parent[parts[i]] = {};
        }
        parent = parent[parts[i]];
    }
    return parent;
};

MYAPP.namespace('MYAPP.Angular');

MYAPP.Angular = (function (global) {
    var app = angular.module('App', ['ngSanitize', 'blockUI']);
    app.config(config);

    config.$inject = [
        'blockUIConfig'
    ];

    function config(blockUiConfig) {

        blockUiConfig
            .template =
            '<div class=\"block-ui-overlay\"></div><div class=\"block-ui-message-container\" aria-live=\"assertive\" aria-atomic=\"true\"><div class=\"block-ui-message\" ng-class=\"$_blockUiMessageClass\">    <svg class="circular" viewBox="25 25 50 50"><circle class="path" cx="50" cy="50" r="20" fill="none" stroke-width="2" stroke-miterlimit="10"/></svg></div></div>';
        blockUiConfig.autoBlock = false;
        blockUiConfig.autoInjectBodyBlock = false;
    }

    app.controller('BalanceController', function ($http, $scope, UpdateBalance, blockUI) {

        this.$onInit = onInit;

        function onInit() {
            $scope.divBlock = blockUI.instances.get('divBlock');
        };

        $scope.balances = {};
            $scope.form = {};

            $scope.get = function () {
                $scope.divBlock.start();
                $http.get('/Api/BalanceApi').then(function (data) {
                    $scope.balances = data;
                }).catch(function() {
                    alert("error");
                }).finally(function () {
                    $scope.divBlock.stop();
                });
            };

        global.onload = function() {
            $scope.get();
        };

        $scope.setScreen = function (index) {
            $scope.current = index;
        };

        $scope.getScreen = function() {
            var result;
            switch ($scope.current) {
                case undefined:
                {
                    result = "/Balance/RouteBalances";
                    $("#home").removeClass('forUpdate');
                    $("#home").addClass('forHome');
                    $("#changeMe").text("Home");
                    break;
                }
                default:
                {
                    result = "/Balance/RouteBalance";
                    $("#home").removeClass('forHome');
                    $("#home").addClass('forUpdate');
                    $("#changeMe").text("Make a Draw");
                    break;
                }
            }
            return result;
        };

        $scope.UpdateScope = function () {
            $scope.balances.data.forEach(function (e) {
                if (e.creditlimit < $("#name").val() && e.Id === $scope.current.Id) {
                    $scope.form.updateADraw.$setValidity("form.updateADraw", false);
                    alert("Balance to withdraw must not be greater than the credit limit");
                }
            });

            if ($scope.form.updateADraw.$valid) {
                $scope.current.Value = $("#name").val();
                UpdateBalance($scope.current);
                global.setTimeout(function () {
                    $scope.get();
                }, 100);
                $scope.form.updateADraw.$setValidity("form.updateADraw", false);
            }
        };

        $scope.getError = function(error) {
            if (angular.isDefined(error)) {
                if (error.required) {
                    return "Please enter a value";
                }
            }
            return "";
        };
    })
    .factory("UpdateBalance", function ($http) {
        return function (sc) {
            $http.post('Api/BalanceApi/UpdateBalanceData', { Id: sc.Id, Balance: sc.Value });
            //.success(function () {
            //    document.location.reload();
            //});
        };
    })
    .$inject = [
        '$http',
        '$scope',
        'UpdateBalance',
        'blockUI'
    ];
}(this));


