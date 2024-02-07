﻿let itemsPerPage = 100;
let currentDate = new Date();
let today = currentDate.toISOString().split('T')[0];

function DownloadList() {
    $('.preload').show();

    let data = {
        dataInizio: $('#dataInizio').val(),
        dataFine: $('#dataFine').val(),
        codiceFiscale: $('#codiceFiscale').val(),
        iuv: $('#iuv').val(),
        worked: 1,
        type: "RichiesteEsitate"
    };

    const iframe = document.createElement('iframe');
    iframe.style.display = 'none';

    document.body.appendChild(iframe);
    $.post("/Action/GetIuvInCsv", data, function (res) {
        iframe.src = res;
    })
        .done(function () {
            $('.preload').hide();
        });
}


function FiltraRichieste() {
    $('.preload').show();
    let data = {
        dataInizio: $('#dataInizio').val(),
        dataFine: $('#dataFine').val(),
        codiceFiscale: $('#codiceFiscale').val(),
        iuv: $('#iuv').val(),
        worked: true,
        page: 1,
        valid: null,
        itemsPerPage: itemsPerPage
    }
    $.post("/Action/FiltraRichieste", data, function (res) {
        let r = JSON.parse(res);
        GetRichieste(r.Result);
        CreatePaginations(data.codiceFiscale, data.iuv, data.dataInizio, data.dataFine, true);
    });
}

function GetRichiestePerPage(p, first) {
    let codiceFiscale = $('#codiceFiscale').val();
    let iuv = $('#iuv').val();
    let dataInizio = $('#dataInizio').val();
    let dataFine = $('#dataFine').val();

    $('.items').removeClass('selected');
    $('.item-' + p).addClass('selected');

    $.get("/Action/GetRichiesteEsitate?page=" + p + "&itemsPerPage=" + itemsPerPage + "&codiceFiscale=" + codiceFiscale + "&iuv=" + iuv + "&dataInizio=" + dataInizio + "&dataFine=" + dataFine, function (res) {
        var r = JSON.parse(res);
        GetRichieste(r.Result);
        if (first)
            CreatePaginations(codiceFiscale, iuv, dataInizio, dataFine, first);

        $('.preload').hide();
    })
}


$(function () {
    $('.preload').show();
    GetRichiestePerPage(1, true);
});

function CreatePaginations(codiceFiscale, iuv, dataInizio, dataFine, first) {
    $.get("/Action/GetRichiesteEsitate?page=1&itemsPerPage=1000000000&codiceFiscale=" + codiceFiscale + "&iuv=" + iuv + "&dataInizio=" + dataInizio + "&dataFine=" + dataFine, function (res) {
        var r = JSON.parse(res);

        var totItems = r.Message;

        $('.pagination').empty();

        if (totItems > 0) {

            let a = "<a onclick='GetRichiestePerPage(1)'><i class='las la-angle-left'></i></a>";

            let nPage = Math.ceil(totItems / itemsPerPage);

            for (var i = 0; i < nPage; i++)
                a += "<a onclick='GetRichiestePerPage(" + (i + 1) + ")' class='items item-" + (i + 1) + "'>" + (i + 1) + "</a>";

            a += "<a onclick='GetRichiestePerPage(" + nPage + ")'><i class='las la-angle-right'></i></a>";

            $('.pagination').append(a);
            if (first)
            {
                $('.items').removeClass('selected');
                $('.item-1').addClass('selected');
            }
        }
    })
}

function EliminaFiltro() {
    $('.preload').show();

    let dataI = $('#dataInizio');
    let dataF = $('#dataFine');
    let codiceFiscale = $('#codiceFiscale');
    let iuv = $('#iuv');
    $.get("/Action/EliminaFiltro", function (res) {
        var r = JSON.parse(res);
        dataI.val(today);
        dataF.val('');
        codiceFiscale.val('');
        iuv.val('');
        GetRichiestePerPage(1, true);
    })
};


function GetRichieste(r) {
    $('.archive-list-accepted').empty();
    if (r != null)
    {
        if (r.length > 0)
        {
            for (var i = 0; i < r.length; i++)
            {
                let rata = "Rata unica";
                let expDate = new Date(r[i].expirationDate);
                let options = { year: 'numeric', month: '2-digit', day: '2-digit' };
                let expDateString = expDate.toLocaleDateString('it-IT', options);

                if (r[i].numeroRata != 0)
                    rata = r[i].numeroRata;
                var li = "<ul>" +
                    "<li>" + r[i].id + "</li>" +
                    "<li>" + r[i].iuv + "</li>" +
                    "<li>" + r[i].codiceIdentificativoUnivocoPagatore + "</li>" +
                    "<li>" + r[i].price + "€</li>" +
                    "<li>" + rata + "</li>" +
                    "<li>" + expDateString + "</li>";

                if (r[i].valid == false || r[i].valid == "" || r[i].valid == null)
                    li += "<li><strong style='color:#EA5555;'><i class='las la-times'></i>&nbsp;NON VALIDATA</strong></li>";

                else
                    li += "<li><strong><i class='las la-check'></i>&nbsp;ESITATA</strong></li>";

                li += "</ul>";

                $('.archive-list-accepted').append(li);
            }
        }
        else
            $('.archive-list-accepted').append("<ul><li style='width:100%; text-align: center; padding:10px'> Nessuna richiesta trovata </li></ul>");
    }
    else
        $('.archive-list-accepted').append("<ul><li style='width:100%; text-align: center; padding:10px'> Nessuna richiesta trovata </li></ul>");

    $('.preload').hide();
}
