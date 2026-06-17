// Minimal Chart.js wrappers called from Blazor via JS interop.
window.sinDineroCharts = (() => {
    const charts = {};

    function renderDoughnut(canvasId, labels, data) {
        const el = document.getElementById(canvasId);
        if (!el || typeof Chart === 'undefined') return;
        if (charts[canvasId]) charts[canvasId].destroy();
        charts[canvasId] = new Chart(el, {
            type: 'doughnut',
            data: {
                labels: labels,
                datasets: [{ data: data }]
            },
            options: {
                responsive: true,
                plugins: { legend: { position: 'right' } }
            }
        });
    }

    // Grouped bar: income vs expense across months.
    function renderTrend(canvasId, labels, incomeData, expenseData) {
        const el = document.getElementById(canvasId);
        if (!el || typeof Chart === 'undefined') return;
        if (charts[canvasId]) charts[canvasId].destroy();
        charts[canvasId] = new Chart(el, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [
                    { label: 'Income', data: incomeData, backgroundColor: '#198754' },
                    { label: 'Expense', data: expenseData, backgroundColor: '#dc3545' }
                ]
            },
            options: {
                responsive: true,
                scales: { y: { beginAtZero: true } },
                plugins: { legend: { position: 'top' } }
            }
        });
    }

    return { renderDoughnut, renderTrend };
})();
