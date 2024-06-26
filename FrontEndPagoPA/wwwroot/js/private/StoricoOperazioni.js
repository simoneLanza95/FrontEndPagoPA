﻿let itemsPerPage = 100;
var currentDate = new Date();
var today = currentDate.toISOString().split('T')[0];

function FiltraRichieste() {
    $('.preload').show();
    let data = {
        dataInizio: $('#dataInizio').val(),
        dataFine: $('#dataFine').val(),
        page: 1,
        itemsPerPage: itemsPerPage
    }
    $.post("/Action/FiltraRichiesteStoricoOperazioni", data, function (res) {
        let r = JSON.parse(res);
        GetRichieste(r.Result);
        CreatePaginations(data.dataInizio, data.dataFine, true);
    });
}

function GetRichiestePerPage(p, first) {
    let dataInizio = $('#dataInizio').val();
    let dataFine = $('#dataFine').val();

    $('.items').removeClass('selected');
    $('.item-' + p).addClass('selected');

    $.get("/Action/GetStoricoOperazioni?page=" + p + "&itemsPerPage=" + itemsPerPage + "&dataInizio=" + dataInizio + "&dataFine=" + dataFine, function (res) {
        var r = JSON.parse(res);
        GetRichieste(r.Result);
        if (first)
            CreatePaginations(dataInizio, dataFine, first);

        $('.preload').hide();
    });
}

function CreatePaginations(dataInizio, dataFine, first) {
    $.get("/Action/GetStoricoOperazioni?page=1&itemsPerPage=1000000000&dataInizio=" + dataInizio + "&dataFine=" + dataFine, function (res) {
        var r = JSON.parse(res);

        var totItems = r.Message;

        $('.pagination').empty();

        if (totItems > 0) {

            let a = "<a onclick='GetRichiestePerPage(1)'><i class='las la-angle-left'></i></a>";

            let nPage = Math.ceil(totItems / itemsPerPage);

            for (var i = 0; i < nPage; i++)
                a += "<a onclick='GetRichiestePerPage(" + (i + 1) + ")' class='items item-" + (i + 1) + "'>" + (i + 1) + "</a>";

            a += "<a onclick='GetRichiestePerPage(" + nPage + ")'><i class='las la-angle-right'></i></a>";

            if (totItems == 1)
                a += "<span>  1 Risultato </span>";
            else
                a += "<span>" + "  " + totItems + " Risultati</span>";

            $('.pagination').append(a);
            if (first) {
                $('.items').removeClass('selected');
                $('.item-1').addClass('selected');
            }
        }
    });
}


$(function () {
    $('.preload').show();
    GetRichiestePerPage(1, true);
});

function EliminaFiltro() {
    $('.preload').show();
    let dataI = $('#dataInizio');
    let dataF = $('#dataFine');
    $.get("/Action/EliminaFiltroStoricoOperazioni", function (res) {
        var r = JSON.parse(res);
        dataI.val(today);
        dataF.val('');
        GetRichiestePerPage(1, true);
    })
};


function GetRichieste(r) {
    $('.operations-history').empty();
    if (r != null) {
        if (r.length > 0) {
            for (var i = 0; i < r.length; i++) {
                let date = new Date(r[i].date);
                let options = { year: 'numeric', month: '2-digit', day: '2-digit' };
                let insDateString = date.toLocaleDateString('it-IT', options);
                var li = "<ul>" +
                    "<li>" + r[i].title + "</li>" +
                    "<li class='text-center'>" + insDateString + "</li>" +
                    "<li class='text-center'>" + r[i].operationType + "</li>";

                if (r[i].bollettino == false || r[i].bollettino == "" || r[i].bollettino == null)
                    li += "<li class='text-center'>IUV</li>";
                else
                    li += "<li class='text-center'>IUV + Bollettino</li>";

                li += "<li class='text-center'><div class='progress-bar'>" +
                    "<div style='width:100%; position:absolute; z-index:200; text-align:center; color:#FFF; font-weight:600;'>" + r[i].workedInstallmentsPercentage + "%</div>" +
                    "<span style='width:" + r[i].workedInstallmentsPercentage + "%'></span></div></li>";
                if (r[i].workedInstallmentsPercentage == 100) {
                    li += "<li class='text-center' title='documento disponibile' onclick='GetCsv(" + r[i].operationId + ")'><i class='bx bx-qr active-btn'></i></li>";

                    if (r[i].bollettino != true) {
                        li += "<li class='text-center' title='nessuna azione possibile'>-</li>";
                    }
                    else {
                        if (r[i].downloadableFile)
                            li += "<li class='text-center' title='documento disponibile' onclick='GetZipFile(" + r[i].operationId + ")'><i class='las la-file-archive active-btn'></i></li>";
                        else
                            li += "<li class='text-center' title='documento non ancora disponibile'><i class='las la-file-archive not-active'></i></li>";
                    }
                }
                else {
                    li += "<li class='text-center' title='documento non ancora disponibile'><i class='bx bx-qr not-active'></i></li>";

                    if (r[i].bollettino != true) {
                        li += "<li class='text-center' title='nessuna azione possibile'>-</li>";
                    }
                    else
                        li += "<li class='text-center' title='documento non ancora disponibile'><i class='las la-file-archive not-active'></i></li>";
                }
                li += "</ul>";

                $('.operations-history').append(li);
            }
        }
        else
            $('.operations-history').append("<ul><li style='width:100%; text-align: center; padding:10px'> Nessuna richiesta trovata </li></ul>");
    }
    else
        $('.operations-history').append("<ul><li style='width:100%; text-align: center; padding:10px'> Nessuna richiesta trovata </li></ul>");

    $('.preload').hide();
}

function GetCsv(id) {
    $('.preload').show();

    const iframe = document.createElement('iframe');
    iframe.style.display = 'none';

    document.body.appendChild(iframe);
    $.get("/Action/GetCsvFromOperation?id=" + id, function (res) {
        iframe.src = res;
    })
        .done(function () {
            $('.preload').hide();
        });
}

function GetZipFile(id) {
    $('.preload').show();

    const iframe = document.createElement('iframe');
    iframe.style.display = 'none';

    document.body.appendChild(iframe);
    $.get("/Action/GetFileFromOperation?id=" + id, function (res) {
        iframe.src = res;
    })
        .done(function () {
            $('.preload').hide();
        });
}