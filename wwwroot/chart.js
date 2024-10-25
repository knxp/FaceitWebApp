// wwwroot/chart.js

// This script is to set up the Chart.js library
// Ensure to include Chart.js CDN in your _Host.cshtml or layout file if not included already

document.addEventListener("DOMContentLoaded", function () {
    // This code will be executed after the DOM is fully loaded
    var ctx = document.getElementById("playerStatsChart").getContext("2d");
    var playerStatsChart = new Chart(ctx, {
        type: "line",
        data: {
            labels: [], // Labels will be set dynamically in PlayerStats.razor
            datasets: [
                {
                    label: "Wins",
                    data: [], // Data will be set dynamically in PlayerStats.razor
                    borderColor: "rgba(75, 192, 192, 1)",
                    backgroundColor: "rgba(75, 192, 192, 0.2)",
                    borderWidth: 2,
                },
                {
                    label: "Losses",
                    data: [], // Data will be set dynamically in PlayerStats.razor
                    borderColor: "rgba(255, 99, 132, 1)",
                    backgroundColor: "rgba(255, 99, 132, 0.2)",
                    borderWidth: 2,
                },
            ],
        },
        options: {
            responsive: true,
            scales: {
                x: {
                    title: {
                        display: true,
                        text: "Games Played",
                    },
                },
                y: {
                    title: {
                        display: true,
                        text: "Count",
                    },
                    beginAtZero: true,
                },
            },
        },
    });
});
