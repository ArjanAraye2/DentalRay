// DentalRay - Study Type Lookup
// Converts the existing New/Edit StudyType text inputs to database-backed selects
// without changing the rest of the current Study form layout.
(function () {
    'use strict';

    let studyTypes = [];

    async function loadStudyTypes() {
        const response = await fetch('/api/studytypes');
        const result = await response.json();
        if (!response.ok || result.success === false)
            throw new Error(result.message || 'دریافت انواع Study انجام نشد.');
        studyTypes = result.studyTypes || [];
    }

    function replaceWithSelect(inputId) {
        const old = document.getElementById(inputId);
        if (!old || old.tagName === 'SELECT') return old;

        const select = document.createElement('select');
        select.id = old.id;
        select.name = old.name || '';
        select.required = true;
        select.className = old.className;
        old.replaceWith(select);
        return select;
    }

    function fill(select) {
        if (!select) return;
        const current = String(select.value || '');
        select.innerHTML = '<option value="">انتخاب نوع Study</option>';
        studyTypes.forEach(type => {
            const option = document.createElement('option');
            option.value = String(type.studyTypeID);
            option.textContent = type.studyTypeName;
            select.appendChild(option);
        });
        if (current) select.value = current;
    }

    function studyTypeId(select) {
        const value = Number(select?.value || 0);
        if (!Number.isInteger(value) || value <= 0)
            throw new Error('نوع Study را انتخاب کنید.');
        return value;
    }

    async function init() {
        const newSelect = replaceWithSelect('newStudyType');
        const editSelect = replaceWithSelect('editStudyType');
        if (!newSelect && !editSelect) return;

        try {
            await loadStudyTypes();
            fill(newSelect);
            fill(editSelect);
        } catch (error) {
            console.error(error);
            [newSelect, editSelect].forEach(x => { if (x) x.disabled = true; });
            return;
        }

        // Existing app.js still builds Study requests. Intercept only those requests
        // and translate the old StudyType payload into the new StudyTypeID contract.
        const originalFetch = window.fetch.bind(window);
        window.fetch = async function (input, init) {
            try {
                const url = typeof input === 'string' ? input : input?.url || '';
                const method = String(init?.method || 'GET').toUpperCase();
                if (/\/api\/radiologystudies(?:\/\d+)?$/i.test(url) && (method === 'POST' || method === 'PUT') && typeof init?.body === 'string') {
                    const body = JSON.parse(init.body);
                    const select = method === 'POST' ? document.getElementById('newStudyType') : document.getElementById('editStudyType');
                    body.studyTypeID = studyTypeId(select);
                    delete body.studyType;
                    init = { ...init, body: JSON.stringify(body) };
                }
            } catch (error) {
                return Promise.reject(error);
            }
            return originalFetch(input, init);
        };

        // When an existing Study is opened for editing, app.js may still assign the
        // old textual value. Keep the select synchronized with StudyTypeID instead.
        const editSection = document.getElementById('editStudySection');
        if (editSection) {
            new MutationObserver(() => {
                if (editSection.classList.contains('hidden')) return;
                const selected = window.selectedStudy;
                if (selected?.studyTypeID && editSelect)
                    editSelect.value = String(selected.studyTypeID);
            }).observe(editSection, { attributes: true, attributeFilter: ['class'] });
        }
    }

    if (document.readyState === 'loading')
        document.addEventListener('DOMContentLoaded', init);
    else
        init();
})();
