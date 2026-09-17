// DentalRay - interactive FDI Dental Chart
// The database stores only FDI tooth numbers. The visual mode is a Frontend preference.
(function () {
    const permanentRows = [
        [18,17,16,15,14,13,12,11,21,22,23,24,25,26,27,28],
        [48,47,46,45,44,43,42,41,31,32,33,34,35,36,37,38]
    ];
    const primaryRows = [
        [55,54,53,52,51,61,62,63,64,65],
        [85,84,83,82,81,71,72,73,74,75]
    ];
    const storageKey = 'dentalray.dentalChartView';

    function toothSvg(number) {
        const type = number % 10;
        let path;
        if (type <= 2) path = 'M13 4 C9 5 8 11 9 18 L11 36 C12 43 17 43 20 34 C23 43 28 43 29 36 L31 18 C32 11 31 5 27 4 C23 2 17 2 13 4 Z';
        else if (type === 3) path = 'M13 5 C9 8 9 13 10 20 L13 37 C14 43 18 43 20 34 C22 43 26 43 27 37 L30 20 C31 13 31 8 27 5 L20 2 Z';
        else path = 'M10 7 C7 12 9 19 11 23 L12 36 C13 43 17 43 20 34 C23 43 27 43 28 36 L29 23 C31 19 33 12 30 7 C27 3 24 5 20 5 C16 5 13 3 10 7 Z';
        return '<svg viewBox="0 0 40 46" aria-hidden="true"><path d="' + path + '"></path></svg>';
    }

    function toothButton(number, selected) {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'tooth-button' + (selected.has(number) ? ' selected' : '');
        button.dataset.tooth = String(number);
        button.setAttribute('aria-label', 'دندان ' + number);
        button.setAttribute('aria-pressed', selected.has(number) ? 'true' : 'false');
        button.innerHTML = '<span class="tooth-shape">' + toothSvg(number) + '</span><span class="tooth-number">' + number + '</span>';
        button.addEventListener('click', function () {
            const isSelected = button.classList.toggle('selected');
            button.setAttribute('aria-pressed', isSelected ? 'true' : 'false');
            updateSelectedSummary(button.closest('.dental-chart'));
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
            row.className = 'dental-chart-row dental-chart-row-' + (index === 0 ? 'upper' : 'lower');
            row.setAttribute('aria-label', index === 0 ? 'فک بالا' : 'فک پایین');
            numbers.forEach(function (number) { row.appendChild(toothButton(number, selected)); });
            section.appendChild(row);
        });
        root.appendChild(section);
    }

    function updateSelectedSummary(root) {
        if (!root) return;
        const target = root.querySelector('.dental-chart-selected');
        if (!target) return;
        const values = Array.from(root.querySelectorAll('.tooth-button.selected')).map(x => x.dataset.tooth);
        target.textContent = values.length ? 'دندان‌های انتخاب‌شده: ' + values.join('، ') : 'هنوز دندانی انتخاب نشده است.';
    }

    function setMode(root, mode) {
        ['linear','anatomical','arch'].forEach(x => root.classList.remove('dental-view-' + x));
        root.classList.add('dental-view-' + mode);
        root.querySelectorAll('.dental-view-button').forEach(button => button.classList.toggle('active', button.dataset.view === mode));
        try { localStorage.setItem(storageKey, mode); } catch (_) { }
    }

    function toolbar(root) {
        const bar = document.createElement('div');
        bar.className = 'dental-chart-toolbar';
        bar.innerHTML = '<strong>شمای دندان‌ها</strong><span class="dental-chart-view-label">نوع نمایش:</span>' +
            '<button type="button" class="dental-view-button" data-view="linear">خطی</button>' +
            '<button type="button" class="dental-view-button" data-view="anatomical">آناتومیک</button>' +
            '<button type="button" class="dental-view-button" data-view="arch">قوسی</button>';
        bar.querySelectorAll('.dental-view-button').forEach(button => button.addEventListener('click', () => setMode(root, button.dataset.view)));
        return bar;
    }

    window.DentalRayDentalChart = {
        render: function (container, selectedTeeth) {
            if (!container) return;
            const selected = new Set((selectedTeeth || []).map(Number));
            container.innerHTML = '';
            container.className = 'dental-chart';
            container.appendChild(toolbar(container));
            const body = document.createElement('div');
            body.className = 'dental-chart-body';
            addSection(body, 'دندان‌های دائمی', permanentRows, selected);
            addSection(body, 'دندان‌های شیری', primaryRows, selected);
            container.appendChild(body);
            const summary = document.createElement('div');
            summary.className = 'dental-chart-selected';
            container.appendChild(summary);
            let mode = 'anatomical';
            try { mode = localStorage.getItem(storageKey) || mode; } catch (_) { }
            if (!['linear','anatomical','arch'].includes(mode)) mode = 'anatomical';
            setMode(container, mode);
            updateSelectedSummary(container);
        },
        getSelected: function (container) {
            if (!container) return [];
            return Array.from(container.querySelectorAll('.tooth-button.selected'))
                .map(button => Number(button.dataset.tooth))
                .sort((a,b) => a-b);
        }
    };
})();