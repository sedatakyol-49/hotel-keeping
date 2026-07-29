<!-- Baslik formati: <tip>(<kapsam>): <ozet>  — orn. feat(reservations): add check-in endpoint -->

## Ne yapildi?
<!-- Kisa ozet: hangi problem cozuldu / hangi ozellik eklendi. -->

## Etkilenen katman(lar)
- [ ] Frontend (`src/frontend`)
- [ ] Backend — Application / Api
- [ ] Domain / Infrastructure (entity + migration)
- [ ] CI/CD, testler, dokumantasyon

## Kontrol listesi
- [ ] `cd src/backend && dotnet test` yesil
- [ ] `cd src/frontend && npm run lint && npm run test && npm run build` yesil
- [ ] Entity degistiyse EF Core migration uretildi (`Persistence/Migrations/`) ve bos veritabanina uygulanabiliyor
- [ ] API sozlesmesi degistiyse `docs/api-contracts.md` guncellendi
- [ ] Yeni endpoint/veri erisimi icin **multi-tenant izolasyon** (HotelId) ve **RBAC** testi var
- [ ] Fatura akisi degistiyse **GoBD** kurallari (finalize sonrasi degistirilemezlik, bosluksuz numara, audit kaydi) korunuyor
- [ ] Kullaniciya gorunen metinler i18n anahtari uzerinden (de/en/tr eksiksiz)
- [ ] Secret / connection string / token commit EDILMEDI
- [ ] Kod ve teknik isimler Ingilizce, aciklama/yorumlar Turkce

## Nasil test edilir?
<!-- Adim adim dogrulama. Ekran goruntusu/istek ornegi ekleyebilirsiniz. -->

## Ilgili konular
<!-- Closes #123 -->
