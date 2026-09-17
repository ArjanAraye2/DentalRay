// DentalRay - simple interactive FDI Dental Chart
// This first version only selects/deselects teeth for a Study.
// Procedure details can be added later without changing this UI contract.
(function () {
    const permanentRows = [
        [18,17,16,15,14,13,12,11,21,22,23,24,25,26,27,28],
        [48,47,46,45,44,43,42,41,31,32,33,34,35,36,37,38]
    ];
    const primaryRows = [
        [55,54,53,52,51,61,62,63,64,65],
        [85,84,83,82,81,71,72,73,74,75]
    ];

    function toothButton(number, selected) {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'tooth-button' + (selected.has(number) ? ' selected' : '');
        button.dataset.tooth = String(number);
        button.setAttribute('aria-pressed', selected.has(number) ? 'true' : 'false');
        button.innerHTML = '<span class="tooth-shape" aria-hidden="true">♢</span><span class="tooth-number">' + number + '</span>';
        button.addEventListener('click', function () {
            const isSelected = button.classList.toggle('selected');
            button.setAttribute('aria-pressed', isSelected ? 'true' : 'false');
        });
        return button;
    }

    function addSection(root, title, rows, selected) {
        const section = document.createElement('div');
        section.className = 'dental-chart-group';
        const heading = document.createElement('div');
        heading.className = 'dental-chart-group-title';
        heading.textContent = title;
        section.appendChild(heading);
        rows.forEach(function (numbers, index) {
            const row = document.createElement('div');
            row.className = 'dental-chart-row';
            row.setAttribute('aria-label', index === 0 ? 'فک بالا' : 'فک پایین');
            numbers.forEach(function (number) { row.appendChild(toothButton(number, selected)); });
            section.appendChild(row);
        });
        root.appendChild(section);
    }

    window.DentalRayDentalChart = {
        render: function (container, selectedTeeth) {
            if (!container) return;
            const selected = new Set((selectedTeeth || []).map(Number));
            container.innerHTML = '';
            container.classList.add('dental-chart');
            addSection(container, 'دندان‌های دائمی', permanentRows, selected);
            addSection(container, 'دندان‌های شیری', primaryRows, selected);
        },
        getSelected: function (container) {
            if (!container) return [];
            return Array.from(container.querySelectorAll('.tooth-button.selected'))
                .map(function (button) { return Number(button.dataset.tooth); })
                .sort(function (a, b) { return a - b; });
        }
    };
})();
