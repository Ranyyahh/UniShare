(function () {
    "use strict";

    document.addEventListener("DOMContentLoaded", function () {
        var dashboardData = {
            registeredStudents: 1248,
            pendingAccounts: 18,
            totalListings: 418,
            pendingListings: 14,
            rentalTransactions: 0,
            rentalRevenue: 0,
            completedTransactions: 0,
            pendingTransactions: 0,
            cancelledTransactions: 0
        };

        function number(value) {
            return Number(value || 0).toLocaleString("en-PH");
        }

        function currency(value) {
            return "₱" + Number(value || 0).toLocaleString("en-PH", {
                minimumFractionDigits: 2,
                maximumFractionDigits: 2
            });
        }

        function updateDashboard(data) {
            document.getElementById("registeredStudents").textContent = number(data.registeredStudents);
            document.getElementById("pendingAccounts").textContent = number(data.pendingAccounts);
            document.getElementById("totalListings").textContent = number(data.totalListings);
            document.getElementById("pendingListings").textContent = number(data.pendingListings);
            document.getElementById("rentalTransactions").textContent = number(data.rentalTransactions);
            document.getElementById("rentalRevenue").textContent = currency(data.rentalRevenue);
            document.getElementById("completedTransactions").textContent = number(data.completedTransactions);
            document.getElementById("pendingTransactions").textContent = number(data.pendingTransactions);
            document.getElementById("cancelledTransactions").textContent = number(data.cancelledTransactions);
        }

        function showToast(message) {
            var toast = document.getElementById("dashboardToast");
            toast.textContent = message;
            toast.classList.add("visible");

            window.setTimeout(function () {
                toast.classList.remove("visible");
            }, 2500);
        }

        var buttons = document.querySelectorAll("[data-placeholder]");

        for (var i = 0; i < buttons.length; i++) {
            buttons[i].addEventListener("click", function () {
                showToast(this.getAttribute("data-placeholder") + " is ready for backend integration.");
            });
        }

        /*
         * Backend placeholder:
         *
         * fetch("/AdminDashboard/GetDashboardMetrics")
         *     .then(function (response) { return response.json(); })
         *     .then(updateDashboard);
         */

        updateDashboard(dashboardData);
    });
}());