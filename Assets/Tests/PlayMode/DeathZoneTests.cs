using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Platformer.Core;
using Platformer.Gameplay;
using Platformer.Mechanics;
using UnityEngine;
using UnityEngine.TestTools;

// ВАРИАНТ 15. Механика: PlayerEnteredDeathZone (+ DeathZone).
// PlayMode-тест: вход игрока в DeathZone планирует событие PlayerEnteredDeathZone.
public class DeathZoneTests
{
    private GameObject playerObj;
    private GameObject zoneObj;

    // Очередь событий Simulation статична — очищаем её до и после каждого теста,
    // чтобы события других тестов не влияли на результат
    [SetUp]
    public void SetUp()
    {
        Simulation.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        Simulation.Clear();
        if (playerObj != null) Object.DestroyImmediate(playerObj);
        if (zoneObj != null) Object.DestroyImmediate(zoneObj);
    }

    // Simulation не предоставляет публичного доступа к очереди событий,
    // поэтому читаем приватное поле eventQueue через рефлексию
    private static object GetEventQueue()
    {
        FieldInfo field = typeof(Simulation).GetField("eventQueue",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(field, "Ошибка: в Simulation не найдено поле eventQueue!");
        return field.GetValue(null);
    }

    private static int GetQueueCount(object queue)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        PropertyInfo prop = queue.GetType().GetProperty("Count", flags);
        if (prop != null) return (int)prop.GetValue(queue);

        FieldInfo field = queue.GetType().GetField("Count", flags);
        Assert.IsNotNull(field, "Ошибка: в очереди событий не найден Count!");
        return (int)field.GetValue(queue);
    }

    private static object PeekQueue(object queue)
    {
        MethodInfo peek = queue.GetType().GetMethod("Peek",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(peek, "Ошибка: в очереди событий не найден метод Peek!");
        return peek.Invoke(queue, null);
    }

    // PLAYMODE ТЕСТ: вход игрока в DeathZone планирует PlayerEnteredDeathZone
    [UnityTest]
    public IEnumerator PlayMode_PlayerEntersDeathZone_SchedulesPlayerEnteredDeathZone()
    {
        // Игрок: объект с коллайдером и реальным скриптом PlayerController.
        // Объект неактивен, чтобы не запускались Awake/Update контроллера —
        // для DeathZone важно только наличие компонента PlayerController
        playerObj = new GameObject("TestPlayer");
        playerObj.SetActive(false);
        Collider2D playerCollider = playerObj.AddComponent<BoxCollider2D>();
        playerObj.AddComponent<PlayerController>();

        // Зона смерти: объект с триггер-коллайдером и реальным скриптом DeathZone
        zoneObj = new GameObject("TestDeathZone");
        BoxCollider2D zoneCollider = zoneObj.AddComponent<BoxCollider2D>();
        zoneCollider.isTrigger = true;
        DeathZone deathZone = zoneObj.AddComponent<DeathZone>();

        object queue = GetEventQueue();
        Assert.AreEqual(0, GetQueueCount(queue), "Ошибка: очередь событий должна быть пустой до входа в зону!");

        // Имитируем вход игрока в триггер: вызываем приватный OnTriggerEnter2D зоны смерти
        zoneObj.SendMessage("OnTriggerEnter2D", playerCollider, SendMessageOptions.RequireReceiver);

        // Проверяем, что в очереди появилось ровно одно событие нужного типа
        Assert.AreEqual(1, GetQueueCount(queue), "Ошибка: вход в DeathZone должен запланировать одно событие!");

        object scheduled = PeekQueue(queue);
        Assert.IsInstanceOf<PlayerEnteredDeathZone>(scheduled,
            "Ошибка: запланированное событие не является PlayerEnteredDeathZone!");

        // Событие должно ссылаться именно на ту зону, в которую вошёл игрок
        Assert.AreSame(deathZone, ((PlayerEnteredDeathZone)scheduled).deathzone,
            "Ошибка: событие ссылается на другую DeathZone!");

        // Пропускаем один кадр для симуляции игрового времени
        yield return null;
    }
}
