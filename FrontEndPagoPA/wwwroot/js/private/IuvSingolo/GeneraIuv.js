﻿$(function () {
    GetUsers();
});

function GetOperationType(id) {
    $('.operationType').empty();
    $('.operationType').append('<option value="">Seleziona un tipo</option>')
    $.get("/Action/GetOperationTypes", function (res) {
        let r = JSON.parse(res);
        $.get("/User/GetOperationTypeSenderUser/" + id, function (resx) {
            let op = JSON.parse(resx).map(a => a.operationTypeId);
            for (var i = 0; i < r.Result.length; i++) {
                if (op.indexOf(r.Result[i].id) >= 0)
                    $('.operationType').append('<option value="' + r.Result[i].id + '">' + r.Result[i].typeName + '</option>')
            }
        });
    })
}

function GetUsers() {
    $.get("/User/GetUsers", function (res) {
        var r = JSON.parse(res);
        for (var i = 0; i < r.length; i++) {
            $('.user').append('<option value="' + r[i].id + '">' + r[i].businessName + '</option>')
        }
    })
}

function GetPreload() {
    $('.preload').show();
}

function GetResult(res) {
    $('.preload').hide();
    var r = JSON.parse(res);
    if (r)
        location.href = '/Action/RequestComplete';
    else
        alert("Si è verificato un errore. Si prega di riprovare");
}

function CreateRatei(n) {
    $('#rate-list').empty();
    if (n > 0)
        for (var i = 0; i < n; i++) {
            $('#rate-list').append('<div class="col-md-3 col-sm-3">' +
                '<label>Scadenza RATA ' + (i + 1) +
                '<input type="date" name="rata' + (i + 1) + '" required /> ' +
                '</label>' +
                '</div>');
        }
}
