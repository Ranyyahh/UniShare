(function () {
    "use strict";

    var toast = document.querySelector(".toast-message");
    var toastTimer;

    function showToast(message) {
        if (!toast) return;
        toast.textContent = message;
        toast.classList.add("is-visible");
        window.clearTimeout(toastTimer);
        toastTimer = window.setTimeout(function () {
            toast.classList.remove("is-visible");
        }, 3200);
    }

    function setError(field, message) {
        var group = field.closest(".field-group") || field.closest(".verification-field");
        var error = group && group.querySelector(".field-error");
        field.setAttribute("aria-invalid", message ? "true" : "false");
        if (error) {
            error.textContent = message || "";
            error.classList.toggle("is-visible", Boolean(message));
        }
    }

    function validateField(field) {
        var value = field.value.trim();
        var message = "";

        if (field.type === "file") {
            if (field.required && !field.files.length) message = "Please upload your institutional ID or COR.";
        } else if (field.required && !value) {
            message = "This field is required.";
        } else if (value && field.type === "email" && !field.validity.valid) {
            message = "Enter a valid email address.";
        } else if (value && field.minLength > 0 && value.length < field.minLength) {
            message = "Please enter at least " + field.minLength + " characters.";
        } else if (field.hasAttribute("data-date-input") && value && !/^(0[1-9]|[12][0-9]|3[01])\/(0[1-9]|1[0-2])\/(19|20)\d{2}$/.test(value)) {
            message = "Use the format dd/mm/yyyy.";
        }

        setError(field, message);
        return !message;
    }

    document.querySelectorAll("form[data-validate='true']").forEach(function (form) {
        form.querySelectorAll("input[required]").forEach(function (field) {
            field.addEventListener("blur", function () {
                if (field.value || field.type === "file") validateField(field);
            });
            field.addEventListener("input", function () {
                if (field.getAttribute("aria-invalid") === "true") validateField(field);
            });
            field.addEventListener("change", function () {
                if (field.type === "file") {
                    var label = form.querySelector("[data-file-name]");
                    if (label) label.textContent = field.files.length ? field.files[0].name : "Upload COR/ID";
                    validateField(field);
                }
            });
        });

        form.addEventListener("submit", function (event) {
            event.preventDefault();
            var valid = true;
            form.querySelectorAll("input[required]").forEach(function (field) {
                if (!validateField(field)) valid = false;
            });
            if (!valid) {
                var firstInvalid = form.querySelector("[aria-invalid='true']");
                if (firstInvalid) firstInvalid.focus();
                return;
            }
            showToast(form.classList.contains("register-form")
                ? "Looks good! Registration will be available when the server is connected."
                : "Looks good! Sign-in will be available when the server is connected.");
        });
    });

    document.querySelectorAll("[data-password-toggle]").forEach(function (button) {
        button.addEventListener("click", function () {
            var input = document.getElementById(button.getAttribute("data-password-toggle"));
            if (!input) return;
            var showing = input.type === "password";
            input.type = showing ? "text" : "password";
            button.setAttribute("aria-label", showing ? "Hide password" : "Show password");
        });
    });

    document.querySelectorAll("[data-demo-link]").forEach(function (link) {
        link.addEventListener("click", function (event) {
            event.preventDefault();
            showToast("This option will be available when the server is connected.");
        });
    });
}());
