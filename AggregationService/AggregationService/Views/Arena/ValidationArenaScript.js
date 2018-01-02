
<script>
    function handleSubmit(f) {
        var toSubmit = true;
        var allowedWords = getAllowedWords();
        var anyFieldsToCheck = document.getElementsByClassName("anyfields");
        for (var i = 0; i < anyFieldsToCheck.length; i++) {
        anyFieldsToCheck[i].style.boxShadow = "";
    if (anyFieldsToCheck[i].value.length < 1 && anyFieldsToCheck[i].value < 1) {
        toSubmit = false;
    anyFieldsToCheck[i].style.boxShadow = "0 0 10px red";
            }
        }
        var fieldsToCheck = document.getElementsByClassName("fields");
        for (var i = 0; i < fieldsToCheck.length; i++) {
        fieldsToCheck[i].style.boxShadow = "";
    if (!isAllowed(fieldsToCheck[i].value, words)) {
        toSubmit = false;
    fieldsToCheck[i].style.boxShadow = "0 0 10px red";
            }
        }
        if (toSubmit) f.submit();
    }

    function getAllowedWords() {
        words = document.getElementById('list').getElementsByTagName('li');
    return words;
    }

    function isAllowed(word, arr) {
        var allowed = false;
        for (var j = 0; j < arr.length; j++) {
            if (arr[j].innerHTML == word) {
        allowed = true;
    }
        }
        return allowed;
    }

    function show() {
        var list = document.getElementById("list");
        if (list.style.display == "none")
            list.style.display = "inline";
        else
            list.style.display = "none";
    }
</script>